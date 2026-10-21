using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    /// <summary>
    /// Experimental Bitcoin day trader for cTrader DEMO accounts only.
    /// The EUR 3 / EUR 10 daily thresholds are best-effort, NOT guaranteed stops.
    /// A single account/symbol instance is required; no martingale or averaging down.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class MariusBitcoinDayTrader : Robot
    {
        private const string BotLabel = "MARIUS_BITCOIN_DAY_DEMO_V1";
        private const double DailyLossEuro = 3.0;
        private const double DailyProfitEuro = 10.0;

        [Parameter("Enable DEMO orders", DefaultValue = true)]
        public bool EnableDemoOrders { get; set; }

        [Parameter("Max EUR risk / trade", DefaultValue = 0.50, MinValue = 0.10, MaxValue = 1.0)]
        public double MaxRiskPerTradeEuro { get; set; }

        [Parameter("EUR fee/slippage reserve", DefaultValue = 0.30, MinValue = 0.10, MaxValue = 1.0)]
        public double CostReserveEuro { get; set; }

        [Parameter("Cooldown minutes", DefaultValue = 2, MinValue = 1, MaxValue = 30)]
        public int CooldownMinutes { get; set; }

        [Parameter("Max hold minutes", DefaultValue = 20, MinValue = 5, MaxValue = 120)]
        public int MaxHoldMinutes { get; set; }

        [Parameter("Max spread / ATR", DefaultValue = 0.35, MinValue = 0.05, MaxValue = 1.0)]
        public double MaxSpreadToAtr { get; set; }

        [Parameter("Stop ATR multiple", DefaultValue = 1.5, MinValue = 1.0, MaxValue = 4.0)]
        public double StopAtrMultiple { get; set; }

        [Parameter("Take-profit / stop", DefaultValue = 1.4, MinValue = 1.0, MaxValue = 3.0)]
        public double RewardRisk { get; set; }

        private ExponentialMovingAverage _emaFast;
        private ExponentialMovingAverage _emaSlow;
        private RelativeStrengthIndex _rsi;
        private AverageTrueRange _atr;
        private DateTime _currentDay;
        private DateTime _nextEntryTime = DateTime.MinValue;
        private bool _halted;
        private bool _closing;

        protected override void OnStart()
        {
            // Deliberate hard locks: this version cannot trade live or non-EUR accounts.
            if (Account.IsLive ||
                !string.Equals(Account.Asset.Name, "EUR", StringComparison.OrdinalIgnoreCase) ||
                !(SymbolName.StartsWith("BTCUSD", StringComparison.OrdinalIgnoreCase) ||
                  SymbolName.StartsWith("XBTUSD", StringComparison.OrdinalIgnoreCase)))
            {
                Print("BLOCKED: DEMO-only, EUR deposit currency and BTCUSD/XBTUSD symbol required.");
                Stop();
                return;
            }

            _emaFast = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 8);
            _emaSlow = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 21);
            _rsi = Indicators.RelativeStrengthIndex(Bars.ClosePrices, 14);
            _atr = Indicators.AverageTrueRange(14, MovingAverageType.Exponential);
            _currentDay = Server.Time.Date;
            Timer.Start(1);
            Positions.Closed += OnPositionClosed;
            Print("Marius Bitcoin DEMO | {0} {1} | EUR -3 stop / +10 stop | enabled={2}",
                  SymbolName, TimeFrame, EnableDemoOrders);
            Print("Only one instance per account. Recommended M1 chart. Uncapped trade count, filtered entries.");
            EnforceRules();
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args.Position.Label == BotLabel && args.Position.SymbolName == SymbolName)
            {
                _nextEntryTime = Server.Time.AddMinutes(CooldownMinutes);
                Print("CLOSED id={0}, net={1:F2} EUR; next entry after {2:HH:mm:ss} UTC",
                      args.Position.Id, args.Position.NetProfit, _nextEntryTime);
                EnforceRules();
            }
        }

        protected override void OnTick()
        {
            EnforceRules();
        }

        protected override void OnTimer()
        {
            EnforceRules();
        }

        protected override void OnBarClosed()
        {
            EnforceRules();
            if (_halted || !EnableDemoOrders || !TradingWindow(Server.Time) ||
                Server.Time < _nextEntryTime || MyPositions().Length != 0 || Bars.Count < 35)
                return;

            double close = Bars.ClosePrices.Last(0);
            double prevHigh = Bars.HighPrices.Last(1);
            double prevLow = Bars.LowPrices.Last(1);
            double fast = _emaFast.Result.Last(0);
            double slow = _emaSlow.Result.Last(0);
            double rsi = _rsi.Result.Last(0);
            double atr = _atr.Result.Last(0);
            double spread = Symbol.Spread;
            if (!Finite(close) || !Finite(fast) || !Finite(slow) ||
                !Finite(rsi) || !Finite(atr) || !Finite(spread) ||
                atr <= 0 || Symbol.PipSize <= 0 || spread < 0)
                return;

            // Momentum / trend / breakout; both long and short are permitted.
            bool buy = fast > slow && close > prevHigh && rsi >= 53 && rsi <= 72;
            bool sell = fast < slow && close < prevLow && rsi <= 47 && rsi >= 28;
            if (!buy && !sell)
                return;
            if (spread / atr > MaxSpreadToAtr)
            {
                Print("SKIP expensive spread: {0:F2} * ATR", spread / atr);
                return;
            }

            double stopPips = Math.Max(atr * StopAtrMultiple, spread * 3.0) / Symbol.PipSize;
            double takePips = stopPips * RewardRisk;
            if (!Finite(stopPips) || stopPips <= 0 || !Finite(takePips))
                return;

            var stats = DailyPnl();
            double remaining = DailyLossEuro + stats.Closed + stats.Floating;
            double budget = Math.Min(MaxRiskPerTradeEuro, remaining - CostReserveEuro);
            if (budget < 0.10)
            {
                Print("SKIP: remaining daily loss allowance too small ({0:F2} EUR).", remaining);
                return;
            }

            // Do NOT round up to minimum volume: skip if broker minimum exceeds budget.
            double rawVolume = Symbol.VolumeForFixedRisk(budget, stopPips, RoundingMode.Down);
            if (!Finite(rawVolume) || rawVolume <= 0)
            {
                Print("SKIP: invalid position volume for risk budget.");
                return;
            }
            double volume = Symbol.NormalizeVolumeInUnits(rawVolume, RoundingMode.Down);
            if (!Finite(volume) || volume < Symbol.VolumeInUnitsMin || volume > Symbol.VolumeInUnitsMax)
            {
                Print("SKIP: broker minimum BTC volume too large for EUR {0:F2} risk.", budget);
                return;
            }

            double estimatedRisk = Symbol.AmountRisked(volume, stopPips);
            var side = buy ? TradeType.Buy : TradeType.Sell;
            double estimatedMargin = Symbol.GetEstimatedMargin(side, volume);
            if (!Finite(estimatedRisk) || !Finite(estimatedMargin) ||
                estimatedRisk <= 0 || estimatedRisk > budget || estimatedMargin <= 0 ||
                estimatedMargin > Account.FreeMargin * 0.50)
            {
                Print("SKIP: risk={0:F2}, budget={1:F2}, margin={2:F2}, free={3:F2}",
                      estimatedRisk, budget, estimatedMargin, Account.FreeMargin);
                return;
            }

            // Recheck right before transmitting a DEMO market order.
            EnforceRules();
            if (Account.IsLive || _halted || MyPositions().Length != 0 ||
                !TradingWindow(Server.Time) || Server.Time < _nextEntryTime)
                return;

            var result = ExecuteMarketOrder(side, SymbolName, volume, BotLabel, stopPips, takePips);
            if (!result.IsSuccessful)
            {
                _nextEntryTime = Server.Time.AddMinutes(10);
                Print("ORDER REJECTED: {0}; 10 minute cool-off", result.Error);
            }
            else
            {
                _nextEntryTime = Server.Time.AddMinutes(CooldownMinutes);
                Print("OPENED DEMO {0} id={1}, risk~{2:F2} EUR, margin~{3:F2} EUR",
                      side, result.Position.Id, estimatedRisk, estimatedMargin);
            }
        }

        private bool TradingWindow(DateTime now)
        {
            // Flatten positions before UTC midnight; avoid assigning late trades to next day.
            return !((now.Hour == 23 && now.Minute >= 50) ||
                     (now.Hour == 0 && now.Minute < 5));
        }

        private Position[] MyPositions()
        {
            return Positions.FindAll(BotLabel, SymbolName);
        }

        private (double Closed, double Floating) DailyPnl()
        {
            double closed = History.FindAll(BotLabel, SymbolName)
                .Where(h => h.ClosingTime.Date == Server.Time.Date)
                .Sum(h => h.NetProfit);
            double floating = MyPositions().Sum(p => p.NetProfit);
            return (closed, floating);
        }

        private void EnforceRules()
        {
            if (Account.IsLive)
                return;

            if (_currentDay != Server.Time.Date)
            {
                if (MyPositions().Length > 0)
                    CloseMine("UTC day changed - no overnight holdings");
                _currentDay = Server.Time.Date;
                _halted = false;
                Print("New UTC day: {0:yyyy-MM-dd}", _currentDay);
            }

            if (!TradingWindow(Server.Time))
                CloseMine("UTC close window");

            foreach (var position in MyPositions())
                if ((Server.Time - position.EntryTime).TotalMinutes >= MaxHoldMinutes)
                    CloseOne(position, "Max holding time");

            var stats = DailyPnl();
            double total = stats.Closed + stats.Floating;
            if (total <= -DailyLossEuro || total >= DailyProfitEuro)
            {
                if (!_halted)
                    Print("DAY HALT: PnL {0:F2} EUR; threshold -3 / +10 EUR", total);
                _halted = true;
            }
            if (_halted)
                CloseMine("Daily profit/loss threshold reached");
        }

        private void CloseOne(Position position, string reason)
        {
            if (_closing) return;
            _closing = true;
            try
            {
                var result = ClosePosition(position);
                Print("CLOSE {0} id={1} ok={2} error={3}",
                      reason, position.Id, result.IsSuccessful, result.Error);
            }
            finally { _closing = false; }
        }

        private void CloseMine(string reason)
        {
            if (_closing) return;
            foreach (var position in MyPositions())
                CloseOne(position, reason);
        }

        private static bool Finite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        protected override void OnError(Error error)
        {
            Print("cTrader trade error: {0}", error.Code);
            _nextEntryTime = Server.Time.AddMinutes(10);
        }

        protected override void OnStop()
        {
            Timer.Stop();
            Positions.Closed -= OnPositionClosed;
            if (!Account.IsLive)
                CloseMine("Bot stopped");
            Print("Marius Bitcoin DEMO stopped.");
        }
    }
}
