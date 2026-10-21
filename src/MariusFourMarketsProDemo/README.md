# Marius Four Markets Pro – Pepperstone/cTrader DEMO

**Only a demo bot.** Blocking real accounts is hard-coded via `Account.IsLive`. "Pro" denotes the project version, not independently verified trading performance. The thresholds have not been optimized against independent historic Pepperstone data.

## Four market profiles
The instance works across **XAUUSD, XAGUSD, BTCUSD and ETHUSD** from one cBot instance, even when attached to a generic chart such as EURUSD H1.
- M5: closed candles, EMA9/EMA21, RSI14 and candle body strength determine timing.
- M15: closed candles and EMA20/EMA50 establish direction and trend strength.
- H1: closed candle and EMA20/EMA50 confirm the trend.
- Different spread-to-ATR, candle-body, EMA-gap and RSI thresholds per instrument.
- Inadequate trend, too large spread, insufficient M5/M15/H1 bars, insufficient margin or unsuitable broker minimum volume means **NO TRADE**.
- May open buys or sells. One open trade per symbol; **max 2 open positions total**. No fixed daily trade count.

## One percent loss risk and three percent profit target – how they work
- `RiskPercent` is fixed at **1% maximum per new position**, based on current account equity (not 1% of market-price change).
- Quality-confirmed entries scale initial maximum risk between 0.50%, 0.65%, 0.80% and 0.95%, or half in defensive learning mode.
- ATR-derived stop distance is sent to the broker as a STOP LOSS at market-order entry; lots are rounded down to broker unit constraints.
- Take-profit pips derive from **3% of the account equity at entry**, converted using the **actual rounded size**: `TP_pips = SL_pips × (equity × 0.03 / estimated_SL_euro_risk)`.
- Example 2,000 EUR equity, actual risk 20 EUR -> target about 60 EUR, TP at 3R. If actual risk 10 EUR, TP still targets ~60 EUR, at 6R – less likely to reach. Broker fills, commission, swaps and FX can change real outcome.
- Both SL and TP are submitted **when opening a trade**, not only checked in 10-second timers.
- If one trade risks 1% and a second trade risks 1%, combined estimated stop risk is at most 2% of equity. This **is not a daily limit**: many losses in one day can accumulate far beyond 2%. Slippage and gaps can break the nominal risk ceiling.
- There is no fixed daily/monthly profit objective. Open positions are attempted to be closed after trading hours (default 07:00–21:00 UTC) or earlier by server-side SL/TP; positions may close at a profit under +3% if session ends. No nightly holding is intended.

## Learning rule
For **each symbol and direction separately** the broker trade history is queried. When at least 12 positions exist, with at least 8 losers and negative combined net return, stricter RSI filters and half the next-trade risk are applied. A single losing position is not enough; the 12-trade window updates over time. This is deterministic risk adaptation, not machine-learning AI.

## Validation and remaining work
GitHub Actions compiles the C# cBot and runs 14 deterministic mathematical checks for 1%-risk, 3%-target conversion, max combined risk and learning thresholds. This does **not** verify profitability, reliability of the broker execution, strategy returns or future signals. A historical backtest using realistic commission, spread, funding and slippage is still required; followed by multiple weeks of demo testing.

## On an iPhone/iPad – Pepperstone-branded cTrader
1. Stop the currently running **old** bot instance; inspect any open positions. Do not run multiple bots on the same demo account.
2. Download the latest successful GitHub Actions artifact `MariusFourMarketsProDemo-algo`, unpack it in Files and import the `.algo` into your Pepperstone cTrader.
3. Start **one** new cloud instance on your **DEMO** account. cTrader can show an inactive preset in addition to the active instance. No need for a second running instance.
4. Journal reports `NO SIGNAL`, `RISK ALLOCATION`, `SIGNAL`, `DEMO ORDER OPENED` or `ORDER REJECTED`.
5. For improvements, send screenshots from **Positionen → Verlauf → Einzeln gewählter Trade** and from the active **cBot → Journal**. A CSV if supported is better for full statistics. Do not share passwords, account access tokens or API keys.
