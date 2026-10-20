using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    // Marius Gold AI Agent v1: explainable adaptive RULES, not an AI language model.
    // HARD LOCK: will never place orders on a live trading account.
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class MariusGoldAIAgent : Robot
    {
        private const string Label = "MARIUS_GOLD_AI_DEMO_V1";

        [Parameter("Enable DEMO orders", DefaultValue = false)]
        public bool EnableDemoOrders { get; set; }

        [Parameter("Risk per trade (%)", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 1.0)]
        public double RiskPercent { get; set; }

        [Parameter("Daily loss limit (%)", DefaultValue = 3.0, MinValue = 0.5, MaxValue = 3.0)]
        public double DailyLossLimitPercent { get; set; }

        [Parameter("Daily profit guide (deposit currency)", DefaultValue = 10.0, MinValue = 0)]
        public double ProfitGuide { get; set; }

        [Parameter("Pause at profit guide", DefaultValue = true)]
        public bool PauseAtGuide { get; set; }

        [Parameter("Max daily trades", DefaultValue = 4, MinValue = 1, MaxValue = 4)]
        public int MaxTrades { get; set; }

        [Parameter("Max spread (pips)", DefaultValue = 50.0, MinValue = 1)]
        public double MaxSpreadPips { get; set; }

        [Parameter("UTC session start hour", DefaultValue = 7, MinValue = 0, MaxValue = 22)]
        public int SessionStart { get; set; }

        [Parameter("UTC session end hour", DefaultValue = 20, MinValue = 1, MaxValue = 23)]
        public int SessionEnd { get; set; }

        [Parameter("Fast EMA", DefaultValue = 20, MinValue = 5)]
        public int FastPeriods { get; set; }

        [Parameter("Slow EMA", DefaultValue = 50, MinValue = 21)]
        public int SlowPeriods { get; set; }

        [Parameter("ATR periods", DefaultValue = 14, MinValue = 5)]
        public int AtrPeriods { get; set; }

        [Parameter("ATR stop multiplier", DefaultValue = 1.8, MinValue = 1, MaxValue = 4)]
        public double StopMultiplier { get; set; }

        [Parameter("Take profit / risk", DefaultValue = 1.8, MinValue = 1, MaxValue = 3)]
        public double RewardRisk { get; set; }

        private ParabolicSAR _sar;
        private ExponentialMovingAverage _fast;
        private ExponentialMovingAverage _slow;
        private AverageTrueRange _atr;
        private DateTime _utcDay;
        private bool _halted;
        private string _haltReason = "";

        protected override void OnStart()
        {
            if (Account.IsLive)
            {
                Print("SAFETY LOCK: Live account not supported. DEMO ONLY.");
                Stop();
                return;
            }

            if (!SymbolName.StartsWith("XAUUSD", StringComparison.OrdinalIgnoreCase) ||
                SessionStart >= SessionEnd || SlowPeriods <= FastPeriods)
            {
                Print("SAFETY LOCK: Requires XAUUSD and valid session/EMA parameters.");
                Stop();
                return;
            }

            _sar = Indicators.ParabolicSAR(0.02, 0.2);
            _fast = Indicators.ExponentialMovingAverage(Bars.ClosePrices, FastPeriods);
            _slow = Indicators.ExponentialMovingAverage(Bars.ClosePrices, SlowPeriods);
            _atr = Indicators.AverageTrueRange(AtrPeriods, MovingAverageType.Exponential);
            _utcDay = Server.Time.Date;
            Timer.Start(30);

            Print("MariusGoldAIAgent v1 | DEMO | Symbol={0} | TimeFrame={1} | orders enabled={2}",
                  SymbolName, TimeFrame, EnableDemoOrders);
            Print("Rules-based adaptive strategy; NOT self-learning AI. Daily profit is NOT guaranteed.");
            CheckLimits();
        }

        protected override void OnTimer()
        {
            if (!Account.IsLive)
                CheckLimits();
        }

        protected override void OnTick()
        {
            if (!Account.IsLive && !IsTradingTime(Server.Time) && MyPositions().Length > 0)
                CloseMine("End of session or weekend");
        }

        protected override void OnBarClosed()
        {
            if (Account.IsLive)
                return;

            CheckLimits();
            if (_halted || !IsTradingTime(Server.Time) ||
                Bars.Count < Math.Max(SlowPeriods, AtrPeriods) + 8)
                return;

            double close = Bars.ClosePrices.Last(0);
            double previous = Bars.ClosePrices.Last(1);
            double sar = _sar.Result.Last(0);
            double oldSar = _sar.Result.Last(1);
            double fast = _fast.Result.Last(0);
            double slow = _slow.Result.Last(0);
            double atr = _atr.Result.Last(0);
            if (!Finite(close) || !Finite(previous) || !Finite(sar) ||
                !Finite(oldSar) || !Finite(fast) || !Finite(slow) ||
                !Finite(atr) || atr <= 0 || close <= 0)
                return;

            bool flipUp = close > sar && previous <= oldSar;
            bool flipDown = close < sar && previous >= oldSar;

            // Exit before looking for entries. Never reverse on the same bar.
            var active = MyPositions();
            if (active.Length != 0)
            {
                foreach (var p in active)
                {
                    if ((p.TradeType == TradeType.Buy && flipDown) ||
                        (p.TradeType == TradeType.Sell && flipUp))
                    {
                        var result = ClosePosition(p);
                        Print("SAR reversal close | position {0} | success={1}", p.Id, result.IsSuccessful);
                    }
                }
                return;
            }

            if (!flipUp && !flipDown)
                return;

            double volatility = 100.0 * atr / close;
            if (volatility < 0.02 || volatility > 1.0)
            {
                Print("SKIP volatility filter ATR%={0:F3}", volatility);
                return;
            }

            var side = flipUp ? TradeType.Buy : TradeType.Sell;
            int sign = flipUp ? 1 : -1;
            int score = 0;
            if (sign * (fast - slow) > 0) score++;
            if (sign * (close - fast) > 0) score++;
            if (sign * (fast - _fast.Result.Last(2)) > 0) score++;
            if (sign * (close - Bars.ClosePrices.Last(3)) > 0) score++;
            if (score < 3)
            {
                Print("SKIP insufficient trend confirmation {0}/4", score);
                return;
            }

            double spreadPips = Symbol.Spread / Symbol.PipSize;
            if (!Finite(spreadPips) || spreadPips > MaxSpreadPips)
            {
                Print("SKIP spread {0:F1} pips", spreadPips);
                return;
            }

            var stats = GetStats();
            if (stats.Trades >= Math.Min(4, MaxTrades))
            {
                Print("SKIP daily trade count reached.");
                return;
            }

            double stopPips = Math.Max(
                atr * StopMultiplier / Symbol.PipSize,
                Math.Max(3.0 * spreadPips, 1.0));
            double takeProfitPips = stopPips * RewardRisk;
            if (!Finite(stopPips) || !Finite(takeProfitPips) ||
                stopPips <= 0 || takeProfitPips <= 0)
                return;

            // Adaptive risk only ever decreases from the user-specified ceiling.
            double riskFraction = Math.Min(RiskPercent, 1.0) / 100.0;
            if (volatility > 0.35)
                riskFraction *= 0.5;
            double riskBudget = Math.Max(0, Account.Equity) * riskFraction;
            if (riskBudget <= 0)
                return;

            double rawVolume = Symbol.VolumeForFixedRisk(riskBudget, stopPips, RoundingMode.Down);
            if (!Finite(rawVolume) || rawVolume < Symbol.VolumeInUnitsMin)
            {
                Print("SKIP min lot size exceeds {0:F2} risk budget; no forced trade.", riskBudget);
                return;
            }

            double volume = Symbol.NormalizeVolumeInUnits(rawVolume, RoundingMode.Down);
            if (!Finite(volume) || volume < Symbol.VolumeInUnitsMin ||
                volume > Symbol.VolumeInUnitsMax)
                return;

            double estimatedRisk = Symbol.AmountRisked(volume, stopPips);
            double margin = Symbol.GetEstimatedMargin(side, volume);
            if (!Finite(estimatedRisk) || !Finite(margin) ||
                estimatedRisk <= 0 || estimatedRisk > riskBudget ||
                margin <= 0 || margin > Account.FreeMargin * 0.7)
            {
                Print("SKIP estimated risk={0:F2}, risk budget={1:F2}, margin={2:F2}",
                      estimatedRisk, riskBudget, margin);
                return;
            }

            Print("SIGNAL {0} score={1}/4 ATR%={2:F3} risk={3:F2} SL={4:F1} TP={5:F1} vol={6}",
                  side, score, volatility, estimatedRisk, stopPips, takeProfitPips, volume);
            if (!EnableDemoOrders)
            {
                Print("SIGNAL ONLY: DEMO order toggle is OFF");
                return;
            }

            // Recheck hard safety gates immediately before any order.
            if (Account.IsLive || _halted || !IsTradingTime(Server.Time) ||
                MyPositions().Length > 0 || GetStats().Trades >= Math.Min(4, MaxTrades))
                return;

            var trade = ExecuteMarketOrder(side, SymbolName, volume, Label, stopPips, takeProfitPips);
            if (trade.IsSuccessful)
                Print("DEMO ORDER ACCEPTED position={0}", trade.Position.Id);
            else
                Print("DEMO ORDER FAILED {0}", trade.Error);
        }

        private bool IsTradingTime(DateTime utc)
        {
            return utc.DayOfWeek != DayOfWeek.Saturday &&
                   utc.DayOfWeek != DayOfWeek.Sunday &&
                   utc.Hour >= SessionStart && utc.Hour < SessionEnd;
        }

        private Position[] MyPositions()
        {
            return Positions.FindAll(Label, SymbolName);
        }

        private (int Trades, double ClosedPnl, double OpenPnl) GetStats()
        {
            var today = Server.Time.Date;
            var history = History.FindAll(Label, SymbolName);
            var closed = history.Where(h => h.ClosingTime.Date == today).ToArray();
            double pnl = closed.Sum(h => h.NetProfit);
            var seenTradeIds = history
                .Where(h => h.EntryTime.Date == today)
                .Select(h => h.PositionId)
                .Distinct();
            var open = MyPositions();
            int tradeCount = seenTradeIds.Union(open
                .Where(p => p.EntryTime.Date == today).Select(p => p.Id)).Count();
            return (tradeCount, pnl, open.Sum(p => p.NetProfit));
        }

        private void CheckLimits()
        {
            if (Account.IsLive)
                return;

            if (Server.Time.Date != _utcDay)
            {
                _utcDay = Server.Time.Date;
                _halted = false;
                _haltReason = "";
                Print("New UTC trading day {0:yyyy-MM-dd}", _utcDay);
            }
            if (!IsTradingTime(Server.Time))
            {
                if (MyPositions().Length > 0)
                    CloseMine("Outside trading session");
                return;
            }

            var stats = GetStats();
            // Approximation assumes no deposits/withdrawals during the demo day.
            double startingBalance = Math.Max(0, Account.Balance - stats.ClosedPnl);
            double lossThreshold = startingBalance * Math.Min(3.0, DailyLossLimitPercent) / 100.0;
            double totalPnl = stats.ClosedPnl + stats.OpenPnl;

            if (lossThreshold > 0 && totalPnl <= -lossThreshold)
            {
                _halted = true;
                _haltReason = "Daily loss threshold";
            }
            if (PauseAtGuide && ProfitGuide > 0 && totalPnl >= ProfitGuide)
            {
                _halted = true;
                _haltReason = "Daily profit guide reached";
            }
            if (_halted && MyPositions().Length > 0)
                CloseMine(_haltReason);
        }

        private void CloseMine(string reason)
        {
            foreach (var p in MyPositions())
            {
                var result = ClosePosition(p);
                Print("CLOSE {0} | position={1} | success={2} | error={3}",
                      reason, p.Id, result.IsSuccessful, result.Error);
            }
        }

        private static bool Finite(double number)
        {
            return !double.IsNaN(number) && !double.IsInfinity(number);
        }

        protected override void OnError(Error error)
        {
            Print("cTrader error {0}", error.Code);
        }

        protected override void OnStop()
        {
            Timer.Stop();
            if (!Account.IsLive)
                CloseMine("Bot stopped");
            Print("Marius Gold AI Agent stopped.");
        }
    }
}
