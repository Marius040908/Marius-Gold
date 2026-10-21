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
    public class MariusRR13Demo : Robot
    {
        private const string BotLabel = "MARIUS_RR13_DEMO_V1";

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

        [Parameter("Risk per trade (%)", DefaultValue = 1.0, MinValue = 1.0, MaxValue = 1.0)]
        public double RiskPercent { get; set; }

        [Parameter("Max simultaneous positions", DefaultValue = 4, MinValue = 1, MaxValue = 4)]
        public int MaxOpenPositions { get; set; }

        [Parameter("UTC session starts", DefaultValue = 7, MinValue = 0, MaxValue = 22)]
        public int SessionStart { get; set; }

        [Parameter("UTC session ends", DefaultValue = 21, MinValue = 1, MaxValue = 23)]
        public int SessionEnd { get; set; }

        [Parameter("Crypto weekends", DefaultValue = true)]
        public bool CryptoWeekends { get; set; }

        [Parameter("Min samples before learning", DefaultValue = 12, MinValue = 12, MaxValue = 100)]
        public int MinLearningTrades { get; set; }

        [Parameter("Max spread as ATR (%)", DefaultValue = 18.0, MinValue = 1.0, MaxValue = 50.0)]
        public double MaxSpreadAtrPercent { get; set; }

        private sealed class MarketState
        {
            public Symbol Symbol;
            public Bars Bars;
            public Bars TrendBars;
            public ExponentialMovingAverage TrendFast;
            public ExponentialMovingAverage TrendSlow;
            public ExponentialMovingAverage Fast;
            public ExponentialMovingAverage Slow;
            public RelativeStrengthIndex Rsi;
            public bool Crypto;
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
        private readonly Dictionary<long, int> _reachedProfitTiers = new Dictionary<long, int>();

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

            if (_markets.Count == 0)
            {
                Print("NO SUPPORTED SYMBOLS: Check exact Pepperstone market names. Bot stopped.");
                Stop();
                return;
            }

            Print("MULTI-ASSET DEMO START | account currency={0} | balance={1:F2} | equity={2:F2} | orders={3}",
                Account.Asset.Name, Account.Balance, Account.Equity, EnableDemoOrders);
            Print("RR13 DEMO | PER TRADE SL RISK <= 1% OF ACCOUNT EQUITY | INITIAL TP AT 3x SL DISTANCE (TARGET ~3% EQUITY IF FULL RISK IS USABLE).");
            Print("NO DAILY OR MONTHLY PROFIT/LOSS HALTS, NO DAILY TRADE COUNT LIMIT; max 4 simultaneous positions (one per market).");
            Print("MAX COMBINED ESTIMATED OPEN-STOP RISK 4% OF EQUITY; real losses can be larger with gaps, spread and slippage.");
            Print("On favorable price moves the bot may trail SL or close early on a confirmed M15 trend reversal; TP is retained on SL modifications.");
            Print("Auto-symbol detection enabled. DEMO orders default ON; select demo account only.");
            Print("ADAPTIVE DEMO v2: M15 trend + M5 entry; independently checks last 12 closed trades for each market and direction.");
            Print("Learns by increasing selectivity and REDUCING risk after >=8 losses AND negative net profit in last 12 completed positions.");
            Print("Statistical learning, NOT machine-learning AI. 1:3 is a target, not a win-rate or profit guarantee.");
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
            var trendBars = MarketData.GetBars(TimeFrame.Minute15, symbol.Name);
            if (bars == null || trendBars == null)
            {
                Print("NO BARS for {0}; skipping.", symbol.Name);
                return;
            }

            var state = new MarketState
            {
                Symbol = symbol,
                Bars = bars,
                TrendBars = trendBars,
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
            if (Account.IsLive)
                return;

            // No DAILY or MONTHLY P&L stop: positions are protected individually.
            // One active position per market; maximum four concurrently.
            if (!WithinSession(Server.Time))
            {
                CloseBotPositions("Outside UTC trading session; no overnight positions");
                return;
            }

            ManageOpenPositions();

            // Dedicated DEMO account: avoid interacting with positions from other robots.
            if (Positions.Any(p => p.Label != BotLabel))
            {
                Print("SAFETY: unrelated open position on account; no new entries.");
                return;
            }

            foreach (var market in _markets)
                Evaluate(market);
        }

        private bool WithinSession(DateTime t) =>
            t.Hour >= SessionStart && t.Hour < SessionEnd;

        private bool WeekdayOrCrypto(MarketState market) =>
            (Server.Time.DayOfWeek != DayOfWeek.Saturday && Server.Time.DayOfWeek != DayOfWeek.Sunday)
            || (market.Crypto && CryptoWeekends);

        private Position[] BotPositions() => Positions.FindAll(BotLabel);

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
            if (m.Bars.Count < 60 || m.TrendBars.Count < 60)
            {
                Print("SKIP {0}: insufficient M5/M15 history (M5={1}, M15={2}; need >=60 each).",
                    m.Symbol.Name, m.Bars.Count, m.TrendBars.Count);
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

            bool m15Up = trendFast > trendSlow && trendClose > trendFast;
            bool m15Down = trendFast < trendSlow && trendClose < trendFast;
            bool buySignal = m15Up && fast > slow && price > fast && price > prev &&
                             rsi >= (buyLearn.Defensive ? 58 : 53) && rsi <= 72 &&
                             (!buyLearn.Defensive || price - fast >= atr * 0.12);
            bool sellSignal = m15Down && fast < slow && price < fast && price < prev &&
                              rsi <= (sellLearn.Defensive ? 42 : 47) && rsi >= 28 &&
                              (!sellLearn.Defensive || fast - price >= atr * 0.12);
            if (!buySignal && !sellSignal)
            {
                Print("NO SIGNAL {0}: M15trend={1}, M5 close={2:F4}, EMA9={3:F4}, EMA21={4:F4}, RSI={5:F1}, buyDefense={6}, sellDefense={7}",
                    m.Symbol.Name, m15Up ? "UP" : m15Down ? "DOWN" : "FLAT",
                    price, fast, slow, rsi, buyLearn.Defensive, sellLearn.Defensive);
                return;
            }

            var direction = buySignal ? TradeType.Buy : TradeType.Sell;
            var selectedLearning = buySignal ? buyLearn : sellLearn;
            double spread = m.Symbol.Spread;
            double spreadPips = spread / m.Symbol.PipSize;
            if (!Finite(spreadPips) || spread < 0 || 100.0 * spread / atr > MaxSpreadAtrPercent)
            {
                Print("SKIP {0}: spread={1:F2} pips exceeds {2:F0}% ATR.", m.Symbol.Name,
                    spreadPips, MaxSpreadAtrPercent);
                return;
            }

            // 1:3 profit-to-risk setting: protective SL ~1% account equity and
            // broker-hosted TP at 3x initial SL distance. Both are approximate.
            double stopPips = Math.Max(1.5 * atr / m.Symbol.PipSize, Math.Max(3 * spreadPips, 2));
            double takePips = stopPips * 3.0;

            var existing = BotPositions();
            // 1% max for each trade, 4% maximum combined open-to-stop exposure.
            // As current account equity changes, new risk budgets adapt.
            double accountEquity = Account.Equity;
            double totalRiskLimit = accountEquity * 0.04;
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
            // A string of bad outcomes triggers HALF-risk mode; it does not
            // increase risk to recover losses or override the 1% ceiling.
            double learningRiskFactor = selectedLearning.Defensive ? 0.5 : 1.0;
            double positionBudget = Math.Min(
                accountEquity * RiskPercent / 100.0 * learningRiskFactor,
                Math.Max(0, totalRiskLimit - openRisk));

            if (!Finite(positionBudget) || positionBudget <= 0)
            {
                Print("SKIP {0}: no safe new risk capacity. Open-stop risk ~{1:F2}, portfolio cap ~{2:F2} {3}.",
                    m.Symbol.Name, openRisk, totalRiskLimit, Account.Asset.Name);
                return;
            }
            Print("RISK ALLOCATION {0}: existing={1}, single-trade max risk~{2:F2}, open-stop risk~{3:F2}, available new risk~{4:F2}, portfolio cap~{5:F2} {6}.",
                m.Symbol.Name, existing.Length, accountEquity*RiskPercent/100.0,
                openRisk, positionBudget, totalRiskLimit, Account.Asset.Name);

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

            Print("SIGNAL {0} {1} | M15 trend confirmed | adaptive defense={2} | RSI={3:F1} | units={4} (~{5:F5} lots) | estimated risk~{6:F2} | target~{7:F2} in account currency before costs | margin~{8:F2} | SL {9:F1}p TP {10:F1}p (3R).",
                m.Symbol.Name, direction, selectedLearning.Defensive, rsi, units,
                units / m.Symbol.LotSize, estimatedRisk, estimatedRisk * 3.0, margin, stopPips, takePips);

            if (!EnableDemoOrders)
            {
                Print("SIGNAL-ONLY {0}: Set 'Enable DEMO orders' = true to send demo orders.", m.Symbol.Name);
                return;
            }

            if (Account.IsLive || !WithinSession(Server.Time) ||
                !m.Symbol.MarketHours.IsOpened() || BotPositions().Any(p => p.SymbolName == m.Symbol.Name))
                return;

            var result = ExecuteMarketOrder(direction, m.Symbol.Name, units, BotLabel, stopPips, takePips);
            if (result.IsSuccessful)
                Print("DEMO ORDER OPENED {0}, position id {1}", m.Symbol.Name, result.Position.Id);
            else
                Print("DEMO ORDER REJECTED {0} | ERROR={1} | margin={2:F2} free={3:F2}.",
                    m.Symbol.Name, result.Error, margin, Account.FreeMargin);
        }

        // Protective trailing can close before the 3R TP; it never moves TP or loosens SL.
        // Adaptive profit-taking on each 10-second timer tick.
        // Requires this cBot instance to keep running: the broker SL protects a trade
        // between ticks, but discretionary trend exits / future SL updates are not server-side.
        // SL always moves in the protective direction; never loosen it.
        private void ManageOpenPositions()
        {
            var positions = BotPositions();
            var liveIds = new HashSet<long>(positions.Select(p => (long)p.Id));
            foreach (var old in _reachedProfitTiers.Keys.Where(id => !liveIds.Contains(id)).ToArray())
                _reachedProfitTiers.Remove(old);

            foreach (var position in positions)
            {
                var market = _markets.FirstOrDefault(m => m.Symbol.Name == position.SymbolName);
                if (market == null || market.Bars.Count < 60 || market.TrendBars.Count < 60 ||
                    !market.Symbol.MarketHours.IsOpened() || !position.StopLoss.HasValue)
                    continue;

                int profitTier = position.NetProfit >= 40 ? 3 :
                                 position.NetProfit >= 25 ? 2 :
                                 position.NetProfit >= 15 ? 1 : 0;
                int previousTier;
                if (!_reachedProfitTiers.TryGetValue(position.Id, out previousTier))
                    previousTier = 0;
                if (profitTier > previousTier)
                {
                    Print("RUNNER MILESTONE {0}: position={1}, floating net P&L={2:F2} {3} (15/25/40 are observations, NOT exit caps).",
                        position.SymbolName, position.Id, position.NetProfit, Account.Asset.Name);
                    _reachedProfitTiers[position.Id] = profitTier;
                }

                double atr = ComputeAtr(market.Bars, 14);
                if (!Finite(atr) || atr <= 0)
                    continue;

                var symbol = market.Symbol;
                bool buy = position.TradeType == TradeType.Buy;
                double marketExitPrice = buy ? symbol.Bid : symbol.Ask;
                double favorableMove = buy ? marketExitPrice - position.EntryPrice :
                                             position.EntryPrice - marketExitPrice;
                double trendFast = market.TrendFast.Result.Last(1);
                double trendSlow = market.TrendSlow.Result.Last(1);
                double prevFast = market.TrendFast.Result.Last(2);
                double prevSlow = market.TrendSlow.Result.Last(2);
                if (!Finite(marketExitPrice) || !Finite(trendFast) || !Finite(trendSlow) ||
                    !Finite(prevFast) || !Finite(prevSlow))
                    continue;

                bool confirmedReversal = buy ? (trendFast < trendSlow && prevFast < prevSlow)
                                              : (trendFast > trendSlow && prevFast > prevSlow);
                if (confirmedReversal)
                {
                    var exit = ClosePosition(position);
                    Print("ADAPTIVE EXIT {0} pos={1}: M15 trend reversal confirmed; success={2}, error={3}, floating net P&L={4:F2}.",
                        symbol.Name, position.Id, exit.IsSuccessful, exit.Error, position.NetProfit);
                    continue;
                }

                // Do not trail until the position has earned at least ~1 ATR in price movement.
                // At ~1.1 ATR, protect a small amount of favorable price movement.
                if (favorableMove < 1.1 * atr)
                    continue;

                bool trendAligned = buy ? trendFast > trendSlow : trendFast < trendSlow;
                // Strong trends receive more room, letting profitable trades run.
                double trailAtrMultiplier = trendAligned ? (favorableMove >= 3.0 * atr ? 1.65 : 1.90) : 1.10;
                double candidate = buy ? marketExitPrice - trailAtrMultiplier * atr :
                                         marketExitPrice + trailAtrMultiplier * atr;
                double entryProtect = buy ? position.EntryPrice + 0.10 * atr :
                                            position.EntryPrice - 0.10 * atr;
                candidate = buy ? Math.Max(candidate, entryProtect) :
                                  Math.Min(candidate, entryProtect);
                candidate = Math.Round(candidate, symbol.Digits);

                // Leave room for spread, broker SL distance and price ticks.
                double safetyGap = Math.Max(3.0 * Math.Max(symbol.Spread, symbol.PipSize), 2.0 * symbol.PipSize);
                double step = Math.Max(0.15 * atr, 2.0 * symbol.PipSize);
                double oldStop = position.StopLoss.Value;
                bool insideMarket = buy ? candidate < symbol.Bid - safetyGap :
                                           candidate > symbol.Ask + safetyGap;
                bool tightens = buy ? candidate >= oldStop + step :
                                      candidate <= oldStop - step;
                if (!Finite(candidate) || !insideMarket || !tightens)
                    continue;

                var modification = ModifyPosition(position, candidate, position.TakeProfit, ProtectionType.Absolute);
                if (modification.IsSuccessful)
                    Print("TRAIL {0} pos={1} | old SL={2:F5} -> new SL={3:F5} | floating net={4:F2} {5} | ATR={6:F5} | aligned trend={7}.",
                        symbol.Name, position.Id, oldStop, candidate, position.NetProfit,
                        Account.Asset.Name, atr, trendAligned);
                else
                    Print("TRAIL REJECTED {0} pos={1} | new SL={2:F5} | error={3}.",
                        symbol.Name, position.Id, candidate, modification.Error);
            }
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
