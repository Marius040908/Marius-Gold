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
    public class MariusUnifiedDayTraderDemo : Robot
    {
        private const string BotLabel = "MARIUS_UNIFIED_DAYTRADER_DEMO_V2";

        [Parameter("Enable DEMO orders", DefaultValue = false)]
        public bool EnableDemoOrders { get; set; }

        [Parameter("Gold symbol", DefaultValue = "XAUUSD")]
        public string GoldName { get; set; }

        [Parameter("Silver symbol", DefaultValue = "XAGUSD")]
        public string SilverName { get; set; }

        [Parameter("Bitcoin symbol", DefaultValue = "BTCUSD")]
        public string BitcoinName { get; set; }

        [Parameter("Ethereum symbol", DefaultValue = "ETHUSD")]
        public string EthereumName { get; set; }

        [Parameter("US stocks (CSV)", DefaultValue = "AAPL,MSFT,NVDA")]
        public string StocksCsv { get; set; }

        [Parameter("News blackout UTC periods", DefaultValue = "")]
        public string NewsBlackoutUtc { get; set; }

        [Parameter("Risk per trade (%)", DefaultValue = 1.0, MinValue = 1.0, MaxValue = 1.0)]
        public double RiskPercent { get; set; }

        [Parameter("Max simultaneous positions", DefaultValue = 2, MinValue = 2, MaxValue = 2)]
        public int MaxOpenPositions { get; set; }

        [Parameter("UTC session starts", DefaultValue = 7, MinValue = 0, MaxValue = 22)]
        public int SessionStart { get; set; }

        [Parameter("UTC session ends", DefaultValue = 21, MinValue = 1, MaxValue = 23)]
        public int SessionEnd { get; set; }


        [Parameter("Min samples before learning", DefaultValue = 12, MinValue = 12, MaxValue = 100)]
        public int MinLearningTrades { get; set; }

        [Parameter("Max spread as ATR (%)", DefaultValue = 18.0, MinValue = 1.0, MaxValue = 50.0)]
        public double MaxSpreadAtrPercent { get; set; }

        private sealed class MarketState
        {
            public Symbol Symbol;
            public Bars Bars;
            public Bars TrendBars;
            public Bars HourBars;
            public ExponentialMovingAverage HourFast;
            public ExponentialMovingAverage HourSlow;
            public ExponentialMovingAverage TrendFast;
            public ExponentialMovingAverage TrendSlow;
            public ExponentialMovingAverage Fast;
            public ExponentialMovingAverage Slow;
            public RelativeStrengthIndex Rsi;
            public bool Crypto;
            public int MarketKind;
            public DateTime LastBarChecked = DateTime.MinValue;
            public bool? LastBuyDefense = null;
            public bool? LastSellDefense = null;
        }

        private sealed class LearningSnapshot
        {
            public bool Defensive;
            public int SampleCount;
            public int Losers;
            public double Net;
        }

        private readonly List<MarketState> _markets = new List<MarketState>();
        private DateTime _warnedDate = DateTime.MinValue;
        private TimeZoneInfo _newYork;

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

            AddMarket(GoldName, false);
            AddMarket(SilverName, false);
            AddMarket(BitcoinName, true);
            AddMarket(EthereumName, true);
            foreach (string stock in (StocksCsv ?? "").Split(','))
                if (!string.IsNullOrWhiteSpace(stock)) AddMarket(stock.Trim(), false, true);
            try { _newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
            catch (TimeZoneNotFoundException)
            {
                try { _newYork = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
                catch { Print("US stock ORB disabled: New York time zone unavailable."); }
            }

            if (_markets.Count == 0)
            {
                Print("NO SUPPORTED SYMBOLS: Check exact Pepperstone market names. Bot stopped.");
                Stop();
                return;
            }

            Print("MULTI-ASSET DEMO START | account currency={0} | balance={1:F2} | equity={2:F2} | orders={3}",
                Account.Asset.Name, Account.Balance, Account.Equity, EnableDemoOrders);
            Print("UNIFIED DEMO: Gold/Silver/BTC/ETH + stocks, M5/M15/H1 and stock ORB, no forced trades.");
            Print("BROKER SL max 1% account equity and 3R take profit; lower risk tiers produce lower account profit targets.");
            Print("At most 2 concurrent and 4 entries/day, 3% daily risk/loss budget. Results not guaranteed.");
            Print("DEMO orders default OFF. No live HTTP news in cTrader cloud; manual blackout parameter only.");
            Print("Learning: 12 completed trades per instrument and direction; defensive after 8 losers and negative net.");
            Print("Learns by increasing selectivity and REDUCING risk after >=8 losses AND negative net profit in last 12 completed positions.");
            Print("Rule adaptation, NOT autonomous AI learning; strategy needs historical and forward validation.");
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

        private void AddMarket(string name, bool crypto, bool stock = false)
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
            var trendBars = MarketData.GetBars(TimeFrame.Minute15, symbol.Name);
            var hourBars = MarketData.GetBars(TimeFrame.Hour, symbol.Name);
            if (bars == null || trendBars == null || hourBars == null)
            {
                Print("NO BARS for {0}; skipping.", symbol.Name);
                return;
            }

            var state = new MarketState
            {
                Symbol = symbol,
                Bars = bars,
                TrendBars = trendBars,
                HourBars = hourBars,
                HourFast = Indicators.ExponentialMovingAverage(hourBars.ClosePrices, 20),
                HourSlow = Indicators.ExponentialMovingAverage(hourBars.ClosePrices, 50),
                MarketKind = stock ? 4 : (crypto ? (name.IndexOf("BTC", StringComparison.OrdinalIgnoreCase) >= 0 ? 2 : 3)
                                    : (name.IndexOf("XAG", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0)),
                TrendFast = Indicators.ExponentialMovingAverage(trendBars.ClosePrices, 20),
                TrendSlow = Indicators.ExponentialMovingAverage(trendBars.ClosePrices, 50),
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
            if (Account.IsLive) return;
            if (DailyLossBreach())
            {
                CloseBotPositions("3% daily drawdown: close owned positions and stop new entries");
                return;
            }
            if (!WithinSession(Server.Time))
            {
                CloseBotPositions("Outside UTC session: day-trading only");
                return;
            }
            if (Positions.Any(p => p.Label != BotLabel))
            {
                Print("SAFETY: unrelated positions on account; skip new orders.");
                return;
            }
            if (TodayEntries() >= 4 || DailyRiskRemaining() <= 0 || IsNewsBlackout(Server.Time))
            {
                if (_warnedDate != Server.Time.Date)
                {
                    Print("ENTRY PAUSE: today count={0}/4, risk allowance={1:F2}; manual news blackout={2}.",
                        TodayEntries(), DailyRiskRemaining(), IsNewsBlackout(Server.Time));
                    _warnedDate = Server.Time.Date;
                }
                return;
            }
            foreach (var market in _markets)
                Evaluate(market);
        }

        private bool WithinSession(DateTime t) =>
            t.DayOfWeek != DayOfWeek.Saturday && t.DayOfWeek != DayOfWeek.Sunday &&
            t.Hour >= SessionStart && t.Hour < SessionEnd;

        private bool WeekdayOrCrypto(MarketState market) =>
            Server.Time.DayOfWeek != DayOfWeek.Saturday && Server.Time.DayOfWeek != DayOfWeek.Sunday;

        private Position[] BotPositions() => Positions.FindAll(BotLabel);

        // Assumes an otherwise unused DEMO account. Daily history is broker-side.
        private double ClosedTodayNet() =>
            History.FindAll(BotLabel).Where(h => h.ClosingTime.Date == Server.Time.Date)
                .Sum(h => h.NetProfit);

        private double ClosedTodayLosses() =>
            History.FindAll(BotLabel).Where(h => h.ClosingTime.Date == Server.Time.Date)
                .GroupBy(h => h.PositionId).Sum(g => Math.Max(0, -g.Sum(h => h.NetProfit)));

        private double EstimatedStartBalance() => Account.Balance - ClosedTodayNet();

        private double DailyRiskRemaining() =>
            MariusUnifiedRules.RemainingDailyBudget(EstimatedStartBalance(), ClosedTodayLosses(), 0);

        private bool DailyLossBreach()
        {
            double start = EstimatedStartBalance();
            double pnl = ClosedTodayNet() + BotPositions().Sum(p => p.NetProfit);
            return !Finite(start) || start <= 0 || !Finite(pnl) || pnl <= -start * 0.03;
        }

        private int TodayEntries()
        {
            var past = History.FindAll(BotLabel)
                .Where(h => h.EntryTime.Date == Server.Time.Date)
                .Select(h => h.PositionId.ToString());
            var live = BotPositions().Where(p => p.EntryTime.Date == Server.Time.Date)
                .Select(p => p.Id.ToString());
            return past.Concat(live).Distinct().Count();
        }

        // UTC format: 2026-10-22T12:00/2026-10-22T14:00;2026-10-23T12:00/2026-10-23T13:00
        // Invalid data is treated as a blackout (fail closed).
        private bool IsNewsBlackout(DateTime now)
        {
            if (string.IsNullOrWhiteSpace(NewsBlackoutUtc)) return false;
            foreach (string entry in NewsBlackoutUtc.Split(';'))
            {
                string[] ends = entry.Trim().Split('/');
                if (ends.Length != 2) return true;
                DateTime start, stop;
                if (!DateTime.TryParseExact(ends[0].Trim(), "yyyy-MM-ddTHH:mm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out start)
                    || !DateTime.TryParseExact(ends[1].Trim(), "yyyy-MM-ddTHH:mm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out stop)
                    || stop <= start) return true;
                if (now >= start && now <= stop) return true;
            }
            return false;
        }

        // History is broker-side; the last N closed positions are reconstructed after cloud restarts.
        // Only this bot's label is used, so other bots do not train the strategy.
        // Partial-close historical records are combined by PositionId to avoid counting one position twice.
        private LearningSnapshot AssessLearning(MarketState market, TradeType side)
        {
            var recent = History.FindAll(BotLabel, market.Symbol.Name, side)
                .GroupBy(x => x.PositionId)
                .Select(g => new {
                    ClosingTime = g.Max(t => t.ClosingTime),
                    Net = g.Sum(t => t.NetProfit)
                })
                .OrderByDescending(x => x.ClosingTime)
                .Take(MinLearningTrades)
                .ToArray();

            int losers = recent.Count(x => x.Net < 0);
            double net = recent.Sum(x => x.Net);
            // At least 12 positions AND >= two thirds losers AND negative net performance.
            // The system only becomes more cautious: it does NOT search for bigger lots.
            bool defensive = recent.Length >= MinLearningTrades &&
                             losers * 3 >= MinLearningTrades * 2 &&
                             net < 0;

            return new LearningSnapshot
            {
                Defensive = defensive, SampleCount = recent.Length, Losers = losers, Net = net
            };
        }

        private void AnnounceLearning(MarketState market, TradeType side, LearningSnapshot snapshot)
        {
            bool? previously = side == TradeType.Buy ? market.LastBuyDefense : market.LastSellDefense;
            if (!previously.HasValue || previously.Value != snapshot.Defensive)
            {
                Print("LEARNING {0} {1}: last {2}/{3} closed positions, losers={4}, net={5:F2} {6}; mode={7}; {8}",
                    market.Symbol.Name, side, snapshot.SampleCount, MinLearningTrades,
                    snapshot.Losers, snapshot.Net, Account.Asset.Name,
                    snapshot.Defensive ? "DEFENSIVE" : "NORMAL",
                    snapshot.Defensive ? "stricter RSI/momentum; HALF risk" : "baseline risk and filters");
                if (side == TradeType.Buy)
                    market.LastBuyDefense = snapshot.Defensive;
                else
                    market.LastSellDefense = snapshot.Defensive;
            }
        }

        private void Evaluate(MarketState m)
        {
            if (m.Bars.Count < 60 || m.TrendBars.Count < 60 || m.HourBars.Count < 60)
            {
                Print("SKIP {0}: insufficient M5/M15/H1 history (min 60 bars each).", m.Symbol.Name);
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
            // M15 values refer only to the last COMPLETED 15-minute candle.
            double trendClose = m.TrendBars.ClosePrices.Last(1);
            double trendFast = m.TrendFast.Result.Last(1);
            double trendSlow = m.TrendSlow.Result.Last(1);
            double hourFast = m.HourFast.Result.Last(1);
            double hourSlow = m.HourSlow.Result.Last(1);
            double hourClose = m.HourBars.ClosePrices.Last(1);
            double barOpen = m.Bars.OpenPrices.Last(1);
            if (!Finite(hourFast) || !Finite(hourSlow) || !Finite(hourClose) || !Finite(barOpen))
            {
                Print("SKIP {0}: invalid H1 or M5 indicator.", m.Symbol.Name);
                return;
            }
            if (!Finite(price) || !Finite(prev) || !Finite(fast) || !Finite(slow) ||
                !Finite(rsi) || !Finite(atr) || !Finite(trendClose) ||
                !Finite(trendFast) || !Finite(trendSlow) ||
                atr <= 0 || m.Symbol.PipSize <= 0)
            {
                Print("SKIP {0}: indicator data invalid.", m.Symbol.Name);
                return;
            }

            // The M15 trend selects the direction, M5 searches for the entry.
            // Evaluate learning from CLOSED positions; 1 losing trade never changes behavior.
            var buyLearn = AssessLearning(m, TradeType.Buy);
            var sellLearn = AssessLearning(m, TradeType.Sell);
            AnnounceLearning(m, TradeType.Buy, buyLearn);
            AnnounceLearning(m, TradeType.Sell, sellLearn);

            // Separate restrictive market profiles. Not backtest-optimized.
            double[] spreadMax = { 12.0, 10.0, 15.0, 14.0, 10.0 };
            double[] trendGapMin = { 0.13, 0.20, 0.16, 0.19, 0.15 };
            double[] candleBodyMin = { 0.09, 0.12, 0.13, 0.15, 0.10 };
            double[] longRsiMin = { 55.0, 57.0, 56.0, 58.0, 55.0 };
            double[] longRsiMax = { 70.0, 69.0, 70.0, 70.0, 70.0 };
            double[] shortRsiMin = { 30.0, 31.0, 30.0, 30.0, 30.0 };
            double[] shortRsiMax = { 45.0, 43.0, 44.0, 42.0, 45.0 };
            int k = m.MarketKind;
            double m15Atr = ComputeAtr(m.TrendBars, 14);
            if (!Finite(m15Atr) || m15Atr <= 0)
                return;
            double spreadPct = 100.0 * m.Symbol.Spread / atr;
            double trendGapAtr = Math.Abs(trendFast - trendSlow) / m15Atr;
            double bodyAtr = Math.Abs(price - barOpen) / atr;
            bool quality = spreadPct <= spreadMax[k] && trendGapAtr >= trendGapMin[k] &&
                           bodyAtr >= candleBodyMin[k];
            bool m15Up = trendFast > trendSlow && trendClose > trendFast;
            bool m15Down = trendFast < trendSlow && trendClose < trendFast;
            bool h1Up = hourFast > hourSlow && hourClose > hourFast;
            bool h1Down = hourFast < hourSlow && hourClose < hourFast;
            bool buySignal = quality && m15Up && h1Up && fast > slow && price > fast && price > prev &&
                             rsi >= longRsiMin[k] + (buyLearn.Defensive ? 3 : 0) && rsi <= longRsiMax[k];
            bool sellSignal = quality && m15Down && h1Down && fast < slow && price < fast && price < prev &&
                              rsi <= shortRsiMax[k] - (sellLearn.Defensive ? 3 : 0) && rsi >= shortRsiMin[k];
            if (k == 4)
            {
                bool orbLong, orbShort;
                if (!StockOpeningRange(m, out orbLong, out orbShort))
                {
                    Print("STOCK WAIT {0}: ORB requires 3 completed opening bars.", m.Symbol.Name);
                    return;
                }
                buySignal = buySignal && orbLong;
                sellSignal = sellSignal && orbShort;
            }
            if (!buySignal && !sellSignal)
            {
                Print("NO SIGNAL {0}: marketProfile={1}, M15={2}, H1={3}, quality={4}, spread/ATR={5:F1}%, gap/ATR={6:F2}, candle/ATR={7:F2}, RSI={8:F1}, defense(B/S)={9}/{10}.",
                    m.Symbol.Name, k, m15Up ? "UP" : m15Down ? "DOWN" : "FLAT",
                    h1Up ? "UP" : h1Down ? "DOWN" : "FLAT",
                    quality, spreadPct, trendGapAtr, bodyAtr, rsi,
                    buyLearn.Defensive, sellLearn.Defensive);
                return;
            }

            var direction = buySignal ? TradeType.Buy : TradeType.Sell;
            var selectedLearning = buySignal ? buyLearn : sellLearn;
            double spread = m.Symbol.Spread;
            double spreadPips = spread / m.Symbol.PipSize;
            if (!Finite(spreadPips) || spread < 0 || spreadPct > Math.Min(MaxSpreadAtrPercent, spreadMax[k]))
            {
                Print("SKIP {0}: spread={1:F2} pips exceeds {2:F0}% ATR.", m.Symbol.Name,
                    spreadPips, MaxSpreadAtrPercent);
                return;
            }

            // Protective Stop Loss and 3% equity Take Profit set server-side on entry.
            double stopPips = Math.Max(1.5 * atr / m.Symbol.PipSize, Math.Max(3 * spreadPips, 2));
            double accountEquity = Account.Equity;
            if (!Finite(accountEquity) || accountEquity <= 0)
                return;
            int strength = 0;
            if (trendGapAtr >= trendGapMin[k] * 1.6) strength++;
            if (bodyAtr >= candleBodyMin[k] * 1.7) strength++;
            if (spreadPct <= spreadMax[k] * 0.4) strength++;
            // Only permitted after ALL entry filters succeed. 0.5%..0.95% risk.
            double riskTierPercent = Math.Min(RiskPercent, 0.50 + 0.15 * strength);
            var existing = BotPositions();
            // At most TWO trades concurrently, each up to 1% of equity:
            // max estimated open-stop downside exposure 2% of equity.
            double totalRiskLimit = accountEquity * 0.02;
            double openRisk = 0.0;
            foreach (var p in existing)
            {
                var sym = Symbols.GetSymbol(p.SymbolName);
                if (sym == null || !p.StopLoss.HasValue || sym.PipSize <= 0)
                {
                    Print("SKIP {0}: unknown stop/risk on existing bot position.", m.Symbol.Name);
                    return;
                }
                double lossDistance = p.TradeType == TradeType.Buy
                    ? p.EntryPrice - p.StopLoss.Value
                    : p.StopLoss.Value - p.EntryPrice;
                double risk = lossDistance > 0
                    ? sym.AmountRisked(p.VolumeInUnits, lossDistance / sym.PipSize)
                    : 0.0;
                if (!Finite(risk) || risk < 0)
                {
                    Print("SKIP {0}: invalid existing position risk calculation.", m.Symbol.Name);
                    return;
                }
                openRisk += risk;
            }
            // Repeated poor results trigger HALF-RISK mode, never revenge trading.
            double learningRiskFactor = selectedLearning.Defensive ? 0.5 : 1.0;
            double positionBudget = Math.Min(
                accountEquity * riskTierPercent / 100.0 * learningRiskFactor,
                Math.Min(Math.Max(0, totalRiskLimit - openRisk),
                         Math.Max(0, DailyRiskRemaining() - openRisk)));

            if (!Finite(positionBudget) || positionBudget <= 0)
            {
                Print("SKIP {0}: no safe new risk capacity. Open-stop risk ~{1:F2}, portfolio cap ~{2:F2} {3}.",
                    m.Symbol.Name, openRisk, totalRiskLimit, Account.Asset.Name);
                return;
            }
            Print("RISK ALLOCATION {0}: existing={1}/2, strength={2}/4, base tier={3:F2}% (max 1%); defensive={4}, risk budget~{5:F2}, open stop risk~{6:F2}, portfolio cap~{7:F2} {8}.",
                m.Symbol.Name, existing.Length, strength, riskTierPercent,
                selectedLearning.Defensive, positionBudget, openRisk, totalRiskLimit, Account.Asset.Name);

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

            // 3R relative to actual stop risk, not a guaranteed 3% of account equity.
            double targetAmount = estimatedRisk * 3.0;
            double takePips = MariusUnifiedRules.TargetPips(stopPips);
            if (!Finite(takePips) || takePips <= 0 || takePips > 1000000)
            {
                Print("SKIP {0}: invalid 3% equity profit target in pips.", m.Symbol.Name);
                return;
            }
            Print("SIGNAL {0} {1}: profile={2}, M5/M15/H1 confirmed, strength={3}/3, risk~{4:F2}, TP aim~{5:F2} account currency, SL={6:F1}p TP={7:F1}p, units={8}, margin~{9:F2}.",
                m.Symbol.Name, direction, k, strength, estimatedRisk,
                targetAmount, stopPips, takePips, units, margin);

            if (!EnableDemoOrders)
            {
                Print("SIGNAL-ONLY {0}: Set 'Enable DEMO orders' = true to send demo orders.", m.Symbol.Name);
                return;
            }

            if (Account.IsLive || !WithinSession(Server.Time) || DailyLossBreach() ||
                TodayEntries() >= 4 || IsNewsBlackout(Server.Time) ||
                DailyRiskRemaining() < estimatedRisk + openRisk ||
                !m.Symbol.MarketHours.IsOpened() || BotPositions().Any(p => p.SymbolName == m.Symbol.Name))
                return;

            var result = ExecuteMarketOrder(direction, m.Symbol.Name, units, BotLabel, stopPips, takePips);
            if (result.IsSuccessful)
                Print("DEMO ORDER OPENED {0}, position id {1}", m.Symbol.Name, result.Position.Id);
            else
                Print("DEMO ORDER REJECTED {0} | ERROR={1} | margin={2:F2} free={3:F2}.",
                    m.Symbol.Name, result.Error, margin, Account.FreeMargin);
        }

        // 15 minute US stock opening range, New York DST automatically handled.
        private bool StockOpeningRange(MarketState m, out bool buy, out bool sell)
        {
            buy = sell = false;
            if (_newYork == null || m.Bars.Count < 20) return false;
            DateTime last = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(m.Bars.OpenTimes.Last(1), DateTimeKind.Utc), _newYork);
            if (last.TimeOfDay < new TimeSpan(9, 45, 0) ||
                last.TimeOfDay >= new TimeSpan(15, 25, 0)) return false;
            double high = double.MinValue, low = double.MaxValue;
            int found = 0;
            for (int i = 1; i < m.Bars.Count && i <= 160; i++)
            {
                DateTime t = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(m.Bars.OpenTimes.Last(i), DateTimeKind.Utc), _newYork);
                if (t.Date != last.Date) break;
                if (t.TimeOfDay >= new TimeSpan(9, 30, 0) && t.TimeOfDay < new TimeSpan(9, 45, 0))
                {
                    found++;
                    high = Math.Max(high, m.Bars.HighPrices.Last(i));
                    low = Math.Min(low, m.Bars.LowPrices.Last(i));
                }
            }
            if (found != 3 || high <= low) return false;
            double close = m.Bars.ClosePrices.Last(1);
            double preceding = m.Bars.ClosePrices.Last(2);
            buy = close > high && preceding <= high;
            sell = close < low && preceding >= low;
            return true;
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
