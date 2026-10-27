# VECTOR GOLD – Pepperstone cTrader DEMO

This is a **separate C# cBot**, not the old cTrader SAR bot and not the previous MetaTrader 5 Python project. It runs on cTrader Cloud only after you install and start a compiled `.algo` file. **GitHub itself is not a trading host.**

## Safety and features
- **Live accounts blocked in code** by `Account.IsLive`. No live override. Demo orders are **OFF by default**.
- XAUUSD (broker suffixes accepted), attached to M15. Uses last **closed H1** EMA20/EMA50 trend and M15 EMA20/EMA50 alignment + five-candle breakout.
- ATR stop loss, initial risk-to-reward take profit, ATR trailing stop, configurable UTC entry window.
- 1% maximum estimated single-trade risk and account-wide open-position veto (no new order while any position exists). Daily approximate loss guard 3%.
- If Pepperstone's minimum volume, spread, free margin or risk budget fails, the bot **skips** the trade. 1,000 EUR may be too little to meet minimum volume safely.
- Can allow DEMO overnight positions by parameter (default OFF).
- This first cloud version is **rules-based**, not trained AI. Economic calendar and automatic self-training are **not implemented**. cTrader Cloud has no HTTP request support.
- Loss limits are estimates. Slippage, fees, deposits/withdrawals, disconnections and cloud restarts can invalidate the estimate. The bot closes its own positions when it stops, but closures can fail if the market is unavailable.
- Test only on a **dedicated Pepperstone cTrader DEMO** account. Do not run two instances on the same account.
- Never commit passwords/API secrets to this **public** GitHub repository.

## iPad installation
1. Open the **Pull Request** for VECTOR GOLD, find the **Checks** tab, and confirm **Build VECTOR GOLD DEMO** is green.
2. Open the successful run and download its `VECTOR-GOLD-DEMO-cTrader` artifact.
3. Unzip in iPad Files, open the resulting `.algo` in **cTrader Mobile**.
4. Select your **Pepperstone DEMO** account, **XAUUSD**, **M15** and **Cloud**.
5. First keep `Enable DEMO orders = false` and review the signal log. Only enable demo orders deliberately after tests.
6. Start the cloud instance. Demo cloud instances have **one concurrent instance** and **seven-day maximum lifetime**, after which a manual restart is needed. Broker limits can vary.

## If it doesn't run
- No .algo? Read the GitHub Actions build log; do not install if red.
- Doesn't import? Confirm the file is .algo, not the source ZIP.
- No signal? Confirm M15, XAUUSD, enough H1 history, broker trading hours and default signal filters.
- Signal but no trade? Check the OFF demo-trading toggle, minimum volume, margin, existing account positions and daily loss limits.
- Using MT5 rather than cTrader? You need a Pepperstone **cTrader** demo account for this version.

Official docs: https://help.ctrader.com/ctrader-algo/documentation/cloud-features/ | https://help.ctrader.com/ctrader-algo/documentation/cbots/cloud-requirements/
