# Marius Bitcoin Day Trader – cTrader DEMO

A standalone **experimental, rules-based** BTCUSD/XBTUSD cBot for Pepperstone/cTrader. It is not self-learning AI, is not tested on historical Bitcoin market data, and does not guarantee profits.

## Fixed daily thresholds

- EUR -3 net PnL (closed trades + open position): close own trades and stop trading until next UTC day.
- EUR +10 net PnL: close own trades and stop trading until next UTC day.
- **Soft thresholds only**. Commission, spread, slippage, gaps, outages and market closure can cause both figures to be exceeded. This is not a guaranteed EUR 3 maximum loss. Counts only trades with this cBot's label, not other bots.
- Only DEMO accounts with EUR deposit currency; live accounts hard blocked. BTCUSD/XBTUSD only (broker suffix allowed).
- Unlimited trades per day **when signals occur**, but at most one bot position at a time. Default 2-minute cool-down.
- Momentum filter: 8/21 EMA trend, RSI confirmation and previous-candle breakout. Long and short, with ATR-derived stop loss/take profit. Recommended start: **M1**.
- Risk per position defaults to approximately EUR 0.50 (before costs), no martingale, no leverage setting changes. Skip an order if broker minimum volume or margin constraint exceeds budget.
- Auto-closes bot positions after 20 minutes and before midnight, with 23:50–00:05 UTC as no-trade period. The broker can reject or delay closes.
- On a DEMO chart, `Enable DEMO orders = true` submits automated demo orders. This setting does NOT remove the live-account block.
- Run **one bot instance per account and symbol**. Multiple instances can defeat the intended risk model.

## Install with iPad

1. In GitHub, open **Actions → Build Marius Bitcoin DEMO** and check the latest run. Only continue if it has succeeded.
2. Download the `MariusBitcoinDayTrader-DEMO` artifact (ZIP), unzip in the iPad Files app, and locate `MariusBitcoinDayTrader.algo`.
3. In cTrader Mobile go to **Algo → cBots → Upload**, import the `.algo` file, and add a cloud instance for a **DEMO account denominated in EUR**.
4. Choose the broker's **BTCUSD** (or XBTUSD) and **M1** timeframe. Start a single instance. The bot trades only when filters, minimum volume and margin checks all pass.
5. Inspect Logs and trades; test several weeks on DEMO plus historical backtesting before considering any further development. Never assume a daily EUR 10 return.

The GitHub repository stores source only; it does not trade. Compilation requires .NET and the cTrader.Automate package. Cloud availability and order parameters depend on your broker.

Official docs: https://help.ctrader.com/ctrader-algo/documentation/visual-studio-ides/ and https://help.ctrader.com/ctrader-algo/documentation/ui-basics/
