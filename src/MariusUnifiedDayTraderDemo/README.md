# Marius Unified DayTrader DEMO v2 (Pepperstone / cTrader)

**Status: compiled demo prototype, no profitability proof, no real-account access.**
This is a new, isolated project. Existing cBots on `main` are not replaced.
The files are in `src/MariusUnifiedDayTraderDemo/`.

## Supports

- One cBot instance watches XAUUSD, XAGUSD, BTCUSD, ETHUSD and up to several comma-separated **broker-available** US equity CFD symbols (default AAPL,MSFT,NVDA).
- Closed M5 candle signals, trend agreement from M15 and H1, EMA, RSI, candle-body and spread/ATR checks.
- US stock trades need **first 15-minute New York opening-range breakout** confirmed by a *closed* M5 candle. The NY time zone is DST-aware if available in cloud; if not, stock signals fail closed.
- Both buy and sell when the broker supports them. No trade is forced.
- All orders are DEMO-only: `Account.IsLive` stops the bot. `Enable DEMO orders` defaults to **false**. When true in a dedicated demo, cTrader orders are submitted automatically without per-order chat approval.
- Max 1% nominal equity-to-stop risk per new position; smaller adaptive risk tiers (0.50%, 0.65%, 0.80%, 0.95%; halved if defensive).
- **3R** target (3 times planned stop distance). This is **not** necessarily +3% of account equity: a position with 0.5% planned equity loss has about +1.5% gross equity upside at its target, *before fees and fill differences*.
- Max **4 new positions per UTC day** and max **2 simultaneously** with 2% combined estimated downside to stops.
- Daily **3% downside budget**: closed losing bot positions count against the allowance even if another trade won. Estimated open stop downside is deducted before admitting each new position. If closed plus floating net P/L drops to <= -3% of reconstructed day-start account balance, owned positions are **attempted** closed and new entries are blocked until the next UTC day.
- Server-side SL/TP attached at order entry, calculated using broker volume minimum/rounding, risk amount and free margin. Skip if minimum broker size cannot safely be afforded.
- M5 journal signal details and error messages; after 12 closed positions for a symbol/direction with >=8 losses and negative net, defensive mode tightens RSI and halves proposed risk. Deterministic conservative adaptation, **not an AI that learns an optimal strategy**.
- Weekends blocked for **all** markets. Position exits attempted outside UTC 07:00-21:00; stock positions additionally attempted closed by 15:55 New York time or 5 minutes before the broker session ends. The bot makes best efforts, **cannot guarantee a position closes during downtime or closed markets**.
- Manual UTC blackout windows for economic/company events (`News blackout UTC periods`). Syntax: `2026-10-22T12:00/2026-10-22T14:00;2026-10-23T13:00/2026-10-23T14:00`. Invalid syntax blocks entries. Empty means **NO news protection**.

## 10,000 simulation paths (offline)

`research/time_machine.py` does a deterministic 10,000-path OHLCV bootstrap and clock-aligned historical replay from 5-minute CSV bars. Separate from the running cBot: **it neither changes bot parameters nor permits live trades**.

Example (historical bars must be CSV with `timestamp,open,high,low,close` and session-local 5-minute timestamps):

```bash
python3 research/time_machine.py --csv /path/to/AAPL-5m.csv --entry 100 --stop 99 --target 103 --side long --at 10:15 --before 2026-10-21 --replays 10000 --seed 19 --cost-r 0.1
python3 -m unittest discover -s research -p 'test_*.py' -v
```

Default US equity sample: 09:30-15:55 session-local. For crypto/metals, supply the clock window and correctly converted file timestamp times e.g. `--session-start 07:00 --at 10:15 --close 21:00`; night-crossing sessions are not supported by this offline script. Minimum 20 prior sessions. A test script provides synthetic fixture checks, *not* real-market profitability evidence. The 10k paths reuse historical candles and are not 10k independent market observations. Commission, gaps and slippage are approximated, not replayed from Pepperstone fills.

True end-to-end cTrader historical backtests with realistic broker symbol, spread, commission, and slippage require **cTrader desktop Windows/Mac**; cTrader Mobile can start cloud instances but cannot run the full cTrader backtesting UI.

## Setup from iPad/iPhone

1. Check green run `Build Unified DayTrader DEMO` on GitHub Actions for this branch.
2. Download artifact `MariusUnifiedDayTraderDemo-algo`, unpack ZIP in iOS Files, import the `.algo` in cTrader.
3. **Stop other trading cBot instances on the same dedicated DEMO account**. Inspect existing open positions separately.
4. Start **one** instance with `Enable DEMO orders = false`, verify symbol names, session clock, missing symbols, journal diagnostics and fixed stops.
5. Only after reviewing logs, deliberately switch `Enable DEMO orders = true` on a DEMO account; no live orders are technically permitted.
6. Before considering any real funds, run out-of-sample backtests, broker-size and margin checks, and several weeks of paper trading. Live support is not implemented.

## Honest limits

The external skill at https://github.com/Jakeschincariol/day-trader-skill is Python and focuses on US shares, while this cBot executes in Pepperstone's cTrader cloud. **cTrader cloud HTTP cannot fetch live Yahoo/SEC/House Clerk data** (cTrader docs: https://help.ctrader.com/ctrader-algo/references/HTTP/Http/). Therefore automated **breaking news, earnings parsing and congressional transaction trackers are NOT inside the cBot**; only the manual blackout parameter is available. Likewise the optional offline Python Time Machine is not an automatic live order gate. The integrated scanner covers **only explicitly configured broker symbols**, not 5,900 stocks.

See `FEATURE_AUDIT.md` for all 11 original skills and conflict decisions. "Unified" describes the safe demo trading core, **not** full implementation of every third-party data service.

## Safety and limitations

- No profit goals or guaranteed losses. Slippage, gaps, market pauses, outages, swaps, and fees can exceed planned 1% and 3% figures.
- The daily-balance reconstruction and entry count assume a **dedicated DEMO account** with one instance, no outside trades, deposits or withdrawals. Other open labels block new entries; historical other-label trades are not counted in the bot's day budget.
- Journal logs are alerts, not mobile push notifications.
- `Account.IsLive` hard-blocks real accounts. Never share secrets in a public repository.
- Never interpret "compiled" or synthetic unit tests as a tested trading edge.
