using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    // Experimental DEMO-only, rules-based multi-asset cBot.
    // Run exactly ONE instance on a dedicated DEMO account.
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class MariusMultiAssetDemo : Robot
    {
        private const string BotLabel = "MARIUS_MULTI_DEMO_V1";

        [Parameter("Enable DEMO orders", DefaultValue = true)]
        public bool EnableDemoOrders { get; set; }

        [Parameter("Gold symbol", DefaultValue = "XAUUSD")]
        public string GoldName { get; set; }

        [Parameter("Silver symbol", DefaultValue = "XAGUSD")]
        public string SilverName { get; set; }

        [Parameter("Bitcoin symbol", DefaultValue = "BTCUSD")]
        public string BitcoinName { get; set; }

        [Parameter("Ethereum symbol", DefaultValue = "ETHUSD")]
        public string EthereumName { get; set; }

        [Parameter("Daily loss limit (%)", DefaultValue = 5.0, MinValue = 5.0, MaxValue = 5.0)]
        public double DailyLossPercent { get; set; }

        [Parameter("Daily profit stop (%)", DefaultValue = 10.0, MinValue = 10.0, MaxValue = 10.0)]
        public double DailyProfitPercent { get; set; }

        [Parameter("Risk per position (%)", DefaultValue = 0.75, MinValue = 0.1, MaxValue = 1.0)]
        public double RiskPercent { get; set; }

        [Parameter("Max simultaneous positions", DefaultValue = 2, MinValue = 1, MaxValue = 2)]
        public int MaxOpenPositions { get; set; }

        [Parameter("UTC session starts", DefaultValue = 7, MinValue = 0, MaxValue = 22)]
        public int SessionStart { get; set; }

        [Parameter("UTC session ends", DefaultValue = 21, MinValue = 1, MaxValue = 23)]
        public int SessionEnd { get; set; }

        [Parameter("Crypto weekends", DefaultValue = true)]
        public bool CryptoWeekends { get; set; }

        [Parameter("Max spread as ATR (%)", DefaultValue = 18.0, MinValue = 1.0, MaxValue = 50.0)]
        public double MaxSpreadAtrPercent { get; set; }

        private sealed class MarketState
        {
            public Symbol Symbol;
            public Bars Bars;
            public ExponentialMovingAverage Fast;
            public ExponentialMovingAverage Slow;
            public RelativeStrengthIndex Rsi;
            public bool Crypto;
            public DateTime LastBarChecked = DateTime.MinValue;
        }

        private readonly List<MarketState> _markets = new List<MarketState>();
        private DateTime _day;
        private bool _halted;
        private string _reason = "";

        protected override void OnStart()
        {
            if (Account.IsLive)
            {
                Print("LIVE ACCOUNT BLOCKED: DEMO ONLY.");
                Stop();
                return;
            }

            if (SessionStart >= SessionEnd)
            {
                Print("INVALID UTC SESSION. Bot stopped.");
                Stop();
                return;
            }

            _day = Server.Time.Date;
            AddMarket(GoldName, false);
            AddMarket(SilverName, false);
            AddMarket(BitcoinName, true);
            AddMarket(EthereumName, true);

            if (_markets.Count == 0)
            {
                Print("NO SUPPORTED SYMBOLS: Check exact Pepperstone market names. Bot stopped.");
                Stop();
                return;
            }

            Print("MULTI-ASSET DEMO START | account currency={0} | balance={1:F2} | equity={2:F2} | orders={3}",
                Account.Asset.Name, Account.Balance, Account.Equity, EnableDemoOrders);
            Print("ONE INSTANCE ONLY. No daily trade count cap. Daily loss -5%; daily profit target +10% of UTC starting balance (NO fixed 10-unit cap).");
            Print("Auto-symbol detection enabled. DEMO orders default ON; select demo account only.");
            Print("NO GUARANTEED RETURNS. NO SELF-LEARNING AI. All four markets share ONE profit/loss limit.");
            Print("WARNING: demo cloud does not persist local storage across restarts. Use a dedicated account; no deposits/other trades.");
            Timer.Start(10);
            OnTimer();
        }

        private Symbol ResolveBrokerSymbol(string requested)
        {
            var exact = Symbols.GetSymbol(requested);
            if (exact != null)
                return exact;

            // Do not infer entirely different instruments from descriptions or aliases.
            var candidates = new List<string>();
            for (int i = 0; i < Symbols.Count; i++)
            {
                string available = Symbols[i];
                if (available.StartsWith(requested, StringComparison.OrdinalIgnoreCase)
                    && available.Length > requested.Length)
                    candidates.Add(available);
            }
            if (candidates.Count == 1)
            {
                var selected = Symbols.GetSymbol(candidates[0]);
                Print("AUTO SYMBOL {0} -> {1} (unique available suffix match).", requested, selected.Name);
                return selected;
            }
            if (candidates.Count > 1)
                Print("AMBIGUOUS {0}: candidates = {1}. Supply exact name once.",
                    requested, string.Join(", ", candidates));
            return null;
        }

        private void AddMarket(string name, bool crypto)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            if (_markets.Any(m => string.Equals(m.Symbol.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                Print("SKIP duplicate symbol {0}", name);
                return;
            }

            // Automatically resolve broker suffixes (e.g. XAUUSD.c) when the match is unique.
            // An exact configured name wins. Multiple fallback candidates -> never guess.
            var symbol = ResolveBrokerSymbol(name);
            if (symbol == null)
            {
                Print("MISSING/AMBIGUOUS SYMBOL {0}: no safe match. Choose exact broker symbol in parameters.", name);
                return;
            }

            var bars = MarketData.GetBars(TimeFrame.Minute5, symbol.Name);
            if (bars == null)
            {
                Print("NO BARS for {0}; skipping.", symbol.Name);
                return;
            }

            var state = new MarketState
            {
                Symbol = symbol,
                Bars = bars,
                Fast = Indicators.ExponentialMovingAverage(bars.ClosePrices, 9),
                Slow = Indicators.ExponentialMovingAverage(bars.ClosePrices, 21),
                Rsi = Indicators.RelativeStrengthIndex(bars.ClosePrices, 14),
                Crypto = crypto
            };
            _markets.Add(state);

            var minVolume = symbol.VolumeInUnitsMin;
            var minMargin = symbol.GetEstimatedMargin(TradeType.Buy, minVolume);
            Print("SYMBOL {0} | min units={1} | lot size={2} | min lot={3:F5} | BUY min margin ~{4:F2} {5} | market open={6}",
                symbol.Name, minVolume, symbol.LotSize, minVolume / symbol.LotSize,
                minMargin, Account.Asset.Name, symbol.MarketHours.IsOpened());
        }

        protected override void OnTimer()
        {
            if (Account.IsLive)
                return;

            if (Server.Time.Date != _day)
            {
                _day = Server.Time.Date;
                _halted = false;
                _reason = "";
                Print("NEW UTC DAY {0:yyyy-MM-dd}. Recheck accumulated daily history.", _day);
            }

            CheckDailyStops();

            if (_halted)
            {
                CloseBotPositions(_reason);
                return;
            }

            // Never leave open positions past our defined daily session.
            if (!WithinSession(Server.Time))
            {
                CloseBotPositions("Outside UTC day session");
                return;
            }

            if (Positions.Any(p => p.Label != BotLabel))
            {
                Print("SAFETY: other open positions detected on account. No NEW bot entries.");
                return;
            }

            foreach (var market in _markets)
            {
                if (_halted)
                    break;

                Evaluate(market);
                CheckDailyStops();
            }
        }

        private bool WithinSession(DateTime t) =>
            t.Hour >= SessionStart && t.Hour < SessionEnd;

        private bool WeekdayOrCrypto(MarketState market) =>
            (Server.Time.DayOfWeek != DayOfWeek.Saturday && Server.Time.DayOfWeek != DayOfWeek.Sunday)
            || (market.Crypto && CryptoWeekends);

        private Position[] BotPositions() => Positions.FindAll(BotLabel);

        private double TodayNetPnl()
        {
            // Historic trades are closed positions; include open net P&L (including currently reported costs).
            var closed = History.FindAll(BotLabel)
                .Where(t => t.ClosingTime.Date == _day)
                .Sum(t => t.NetProfit);
            return closed + BotPositions().Sum(p => p.NetProfit);
        }

        private double TodayStartingBalance()
        {
            // Reconstructed from closed bot P&L to survive ordinary restarts while history is present.
            // Requires a DEDICATED demo account with no manual trading/deposits/withdrawals.
            var closed = History.FindAll(BotLabel)
                .Where(t => t.ClosingTime.Date == _day)
                .Sum(t => t.NetProfit);
            return Account.Balance - closed;
        }

        private void CheckDailyStops()
        {
            double start = TodayStartingBalance();
            double pnl = TodayNetPnl();
            if (!Finite(start) || start <= 0 || !Finite(pnl))
            {
                _halted = true;
                _reason = "Invalid account/history data";
            }
            else if (pnl <= -start * DailyLossPercent / 100.0)
            {
                _halted = true;
                _reason = "DAILY LOSS -5% triggered";
            }
            else if (pnl >= start * DailyProfitPercent / 100.0)
            {
                _halted = true;
                _reason = "DAILY PROFIT +10% target reached";
            }

            if (_halted)
                Print("HALT {0} | start ~{1:F2} | net P&L {2:F2} | deposit currency={3}",
                    _reason, start, pnl, Account.Asset.Name);
        }

        private void Evaluate(MarketState m)
        {
            if (m.Bars.Count < 60)
            {
                Print("SKIP {0}: only {1} bars, waiting for >=60.", m.Symbol.Name, m.Bars.Count);
                return;
            }

            // Timer may run many times; process each completed M5 bar once per market.
            var completedTime = m.Bars.OpenTimes.Last(1);
            if (completedTime == m.LastBarChecked)
                return;
            m.LastBarChecked = completedTime;

            if (!WeekdayOrCrypto(m))
            {
                Print("SKIP {0}: weekend restrictions.", m.Symbol.Name);
                return;
            }
            if (!m.Symbol.MarketHours.IsOpened())
            {
                Print("SKIP {0}: broker market closed.", m.Symbol.Name);
                return;
            }
            if (BotPositions().Any(p => p.SymbolName == m.Symbol.Name))
            {
                Print("SKIP {0}: bot position already open.", m.Symbol.Name);
                return;
            }
            if (BotPositions().Length >= MaxOpenPositions)
            {
                Print("SKIP {0}: maximum {1} simultaneous positions.", m.Symbol.Name, MaxOpenPositions);
                return;
            }

            double price = m.Bars.ClosePrices.Last(1);
            double prev = m.Bars.ClosePrices.Last(2);
            double fast = m.Fast.Result.Last(1);
            double slow = m.Slow.Result.Last(1);
            double rsi = m.Rsi.Result.Last(1);
            double atr = ComputeAtr(m.Bars, 14);
            if (!Finite(price) || !Finite(prev) || !Finite(fast) || !Finite(slow) ||
                !Finite(rsi) || !Finite(atr) || atr <= 0 || m.Symbol.PipSize <= 0)
            {
                Print("SKIP {0}: indicator data invalid.", m.Symbol.Name);
                return;
            }

            // No rare SAR flip required: trade only sustained trend with momentum/RSI confirmation.
            bool buySignal = fast > slow && price > fast && price > prev && rsi >= 53 && rsi <= 72;
            bool sellSignal = fast < slow && price < fast && price < prev && rsi <= 47 && rsi >= 28;
            if (!buySignal && !sellSignal)
            {
                Print("NO SIGNAL {0}: close={1:F4}, EMA9={2:F4}, EMA21={3:F4}, RSI={4:F1}",
                    m.Symbol.Name, price, fast, slow, rsi);
                return;
            }

            var direction = buySignal ? TradeType.Buy : TradeType.Sell;
            double spread = m.Symbol.Spread;
            double spreadPips = spread / m.Symbol.PipSize;
            if (!Finite(spreadPips) || spread < 0 || 100.0 * spread / atr > MaxSpreadAtrPercent)
            {
                Print("SKIP {0}: spread={1:F2} pips exceeds {2:F0}% ATR.", m.Symbol.Name,
                    spreadPips, MaxSpreadAtrPercent);
                return;
            }

            // Stop is ATR-based and wider than 3x spread; avoid false precision.
            double stopPips = Math.Max(1.5 * atr / m.Symbol.PipSize, Math.Max(3 * spreadPips, 2));
            double takePips = stopPips * 1.5;
            double start = TodayStartingBalance();
            double dailyPnl = TodayNetPnl();
            double remainingDailyLoss = start * DailyLossPercent / 100.0 + dailyPnl;
            double positionBudget = Math.Min(Account.Equity * RiskPercent / 100.0,
                                             Math.Max(0, remainingDailyLoss * 0.45));

            // Avoid risk piling up through correlated positions (e.g. BTC and ETH).
            double openRisk = 0.0;
            foreach (var p in BotPositions())
            {
                var sym = Symbols.GetSymbol(p.SymbolName);
                if (sym == null || !p.StopLoss.HasValue)
                {
                    Print("SKIP {0}: existing position has unknown stop/risk.", m.Symbol.Name);
                    return;
                }
                double pipsAtStop = Math.Abs(p.EntryPrice - p.StopLoss.Value) / sym.PipSize;
                openRisk += sym.AmountRisked(p.VolumeInUnits, pipsAtStop);
            }
            double portfolioBudget = Math.Max(0, start * 0.035 - openRisk);
            positionBudget = Math.Min(positionBudget, portfolioBudget);

            if (positionBudget <= 0)
            {
                Print("SKIP {0}: daily/portfolio risk budget depleted.", m.Symbol.Name);
                return;
            }

            double minVol = m.Symbol.VolumeInUnitsMin;
            double minRisk = m.Symbol.AmountRisked(minVol, stopPips);
            double minMargin = m.Symbol.GetEstimatedMargin(direction, minVol);
            if (!Finite(minRisk) || !Finite(minMargin) || minRisk <= 0 || minMargin <= 0 ||
                minRisk > positionBudget || minMargin > Account.FreeMargin * 0.75)
            {
                Print("SKIP {0}: MIN LOT NOT AFFORDABLE. min lot={1:F5}, min SL risk={2:F2}, allowed risk={3:F2}, min margin={4:F2}, free margin={5:F2} {6}.",
                    m.Symbol.Name, minVol / m.Symbol.LotSize, minRisk, positionBudget,
                    minMargin, Account.FreeMargin, Account.Asset.Name);
                return;
            }

            var rawUnits = m.Symbol.VolumeForFixedRisk(positionBudget, stopPips, RoundingMode.Down);
            double units = m.Symbol.NormalizeVolumeInUnits(rawUnits, RoundingMode.Down);
            if (!Finite(units) || units < minVol)
            {
                Print("SKIP {0}: rounded order below broker minimum.", m.Symbol.Name);
                return;
            }

            units = Math.Min(units, m.Symbol.VolumeInUnitsMax);
            double estimatedRisk = m.Symbol.AmountRisked(units, stopPips);
            double margin = m.Symbol.GetEstimatedMargin(direction, units);
            if (!Finite(estimatedRisk) || !Finite(margin) || estimatedRisk <= 0 ||
                estimatedRisk > positionBudget || margin <= 0 || margin > Account.FreeMargin * 0.75)
            {
                Print("SKIP {0}: estimated risk={1:F2} budget={2:F2} margin={3:F2} free={4:F2}.",
                    m.Symbol.Name, estimatedRisk, positionBudget, margin, Account.FreeMargin);
                return;
            }

            Print("SIGNAL {0} {1} | RSI {2:F1} | units {3} (~{4:F5} lots) | risk ~{5:F2} | margin ~{6:F2} | SL {7:F1}p TP {8:F1}p.",
                m.Symbol.Name, direction, rsi, units, units / m.Symbol.LotSize, estimatedRisk,
                margin, stopPips, takePips);

            if (!EnableDemoOrders)
            {
                Print("SIGNAL-ONLY {0}: Set 'Enable DEMO orders' = true to send demo orders.", m.Symbol.Name);
                return;
            }

            CheckDailyStops();
            if (_halted || Account.IsLive || !WithinSession(Server.Time) ||
                !m.Symbol.MarketHours.IsOpened() || BotPositions().Any(p => p.SymbolName == m.Symbol.Name))
                return;

            var result = ExecuteMarketOrder(direction, m.Symbol.Name, units, BotLabel, stopPips, takePips);
            if (result.IsSuccessful)
                Print("DEMO ORDER OPENED {0}, position id {1}", m.Symbol.Name, result.Position.Id);
            else
                Print("DEMO ORDER REJECTED {0} | ERROR={1} | margin={2:F2} free={3:F2}.",
                    m.Symbol.Name, result.Error, margin, Account.FreeMargin);
        }

        private static double ComputeAtr(Bars bars, int periods)
        {
            double total = 0;
            for (int i = 1; i <= periods; i++)
            {
                double hi = bars.HighPrices.Last(i);
                double lo = bars.LowPrices.Last(i);
                double previous = bars.ClosePrices.Last(i + 1);
                total += Math.Max(hi - lo, Math.Max(Math.Abs(hi - previous), Math.Abs(lo - previous)));
            }
            return total / periods;
        }

        private void CloseBotPositions(string reason)
        {
            foreach (var position in BotPositions())
            {
                var instrument = Symbols.GetSymbol(position.SymbolName);
                if (instrument == null || !instrument.MarketHours.IsOpened())
                {
                    Print("CANNOT CLOSE {0}: market closed; retry next timer.", position.SymbolName);
                    continue;
                }
                var result = ClosePosition(position);
                Print("CLOSE {0} pos={1} | reason={2} | success={3} error={4}",
                    position.SymbolName, position.Id, reason, result.IsSuccessful, result.Error);
            }
        }

        protected override void OnError(Error error)
        {
            Print("CTRADER ERROR: {0}", error.Code);
        }

        protected override void OnStop()
        {
            Timer.Stop();
            Print("BOT STOPPED. Positions are NOT automatically closed simply because the instance stops.");
        }

        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }
}
