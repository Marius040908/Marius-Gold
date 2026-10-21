# Third-party 11-skill audit and precedence rules (21 October 2026)

Original source: https://github.com/Jakeschincariol/day-trader-skill (MIT).
This is an **independent C# implementation of overlapping concepts**, not a copy of the Python skill package.

| Source skill | Status in Unified cBot | Details |
| --- | --- | --- |
| trader-scanner | **PARTIAL** | Watches explicitly configured Pepperstone-supported metals, crypto, and equity symbols, NOT 5,900 US listings. M5/M15/H1 technical screening; equities add a 15m opening-range breakout. |
| trader-news | **NOT AUTOMATED** | Cloud cTrader has no supported Http calls. A manually maintained UTC economic/news blackout prevents entries. No Yahoo/SEC polling. |
| trader-entry | **YES, ADAPTED** | Enters only on completed M5 candles; trend/filter confirmation required; stocks require ORB. |
| trader-exit | **YES, ADAPTED** | Broker SL using ATR/spread, fixed 3R TP at entry, best-effort intraday exits. |
| trader-timemachine | **SEPARATE OFFLINE** | `research/time_machine.py`: 10k seeded 5m Monte Carlo + clock-time historical replay from external CSV. Not the entire source skill's ORB pattern-replay and not connected as live order gate. |
| trader-risk | **YES, ADAPTED** | <=1% planned per trade, 3% day budget, <=2% open-risk, <=4 entries/day, market closed/news periods, no real accounts. |
| trader-budget | **YES, ADAPTED** | Broker minimum lot, margin, and stop-distance sizing; trade skipped if risk exceeds available budget. |
| trader-broker | **YES, ADAPTED** | Orders through Pepperstone/cTrader DEMO only; user opted into auto demo trading, instead of source skill's per-order Alpaca manual approval. |
| trader-alerts | **PARTIAL** | cTrader Journal diagnostics and broker server-side stops; no mobile push/SMS service. |
| trader-analyst | **NOT IN BOT** | No SEC/company financial statement API inside cloud cBot. |
| trader-pelosi | **NOT IN BOT** | No congressional disclosure scraper inside cloud cBot; delayed disclosures offer no validated 5m signal edge. |

## Conflicting definitions resolved

1. **3% profit of whole account vs 3R target:** 3R selected for stable probability/risk arithmetic; the previous four-market cBot used a larger 3%-equity take-profit even if risk tier was <1%, creating e.g. 6R at 0.5% planned loss. This can be sensible in some strategies but has no supporting out-of-sample evidence here, so use the simpler consistent 3R. Not proof that 3R earns more.
2. **No trade limit vs 4 trades/day:** hard cap 4, skip weak/no signals. Conservatism wins over trade frequency.
3. **Weekend crypto vs no positions over weekends:** block weekends for every market and attempt intraday flattening. Execution cannot be guaranteed when a venue is closed.
4. **Open risk vs daily loss:** BOTH independent controls. <=2% concurrent planned stop downside; <=3% cumulative daily loss allocation, and 3% floating+closed net drawdown stop.
5. **External skill manual broker approval vs automatic demo trading:** explicit global opt-in to automatic demo only; account.IsLive stops real-money order routing.
6. **Self-learning vs fixed rules:** conservative loss-history adaptation, never automatic lot escalation or unsupervised model retraining.
7. **Free unofficial feeds vs native broker data:** use cTrader's broker-supplied M5/M15/H1 data for execution timing and risk; external Yahoo/SEC research stays separate, preventing stale Yahoo quotes being used as execution prices.
8. **Research simulation vs trading itself:** Monte Carlo is *research only* until independently verified; simulated outcomes are not treated as guaranteed expected return or a passing trade score.

## Validation performed vs missing

- Existing four-market baseline compiled on GitHub; new project has its own GitHub Actions build and isolated deterministic C# risk-math tests.
- Offline research script has deterministic synthetic-data unit tests, running in a separate GitHub Actions job.
- **Still missing:** Pepperstone/cTrader backtests on actual requested symbols, long forward demo sample, commission/slippage/market-gap sensitivity, risk-of-ruin estimate, tests with outages, and a real SEC/news/economic feed suitable for cTrader's cloud limitations.
- The combined bot is **not claimed profitable** and is deliberately isolated in a feature branch pending review.
