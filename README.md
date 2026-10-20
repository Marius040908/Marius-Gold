# Marius Gold AI Agent — Pepperstone / cTrader DEMO

**Status:** Source code and GitHub Actions build workflow are available. Verify the CI build status on GitHub Actions before attempting to install. This is experimental **demo-only** software, not a profitable or backtested system.

**Important:** "AI Agent" is this project's name. Version 1 is an explainable *rules-based* adaptive strategy, **not** a neural network or a self-training agent. It cannot read news or Bitcoin prices.

## Files

- \`src/MariusGoldAI/MariusGoldAIAgent.cs\` — cTrader C# cBot
- \`src/MariusGoldAI/MariusGoldAI.csproj\` — .NET / cTrader.Automate project
- \`.github/workflows/build.yml\` — GitHub Actions build and .algo artifact
- \`.gitignore\` — excludes credentials and build output

## Strategy and limits

- Only XAUUSD (broker suffixes accepted). Start testing on an M15 chart.
- Long/short entries on a *completed candle* with a Parabolic SAR flip, supported by at least 3 of 4 confirmations from EMA20/EMA50, price vs EMA20, EMA20 direction and three-bar momentum.
- ATR stop-loss, default 1.8:1 take-profit / stop ratio, and volatility filter. When volatility is high the allowed risk **decreases**.
- 1% estimated equity risk per trade at most. If minimum broker volume violates that risk budget, the order is skipped.
- Maximum 4 trades per UTC day, observed 3% daily loss limit, one open bot position at a time. The loss limit is not guaranteed: slippage, gaps or outages can produce larger losses.
- The EUR 10 guide is **not a target to chase** and not a forecast: by default it pauses new orders after the guide has been met. The amount is in the account deposit currency.
- Session Mon–Fri 07:00–20:00 UTC; bot attempts to close its own positions outside the session. Closures cannot be guaranteed during outages or when the market is closed.
- **Hard live-account block:** \`Account.IsLive\` stops this bot. No configuration can enable live trading with the current code.
- Default \`Enable DEMO orders = false\` means signals only. The user must explicitly enable demo orders in their demo instance.
- Trade labels are scoped to this cBot. Run **only one instance** per account/symbol; other bots' positions are not controlled.
- **No guarantee of profits or risk limits.** A 100 EUR demo account may make zero trades if the broker minimum trade size is too large.
- The bot does not handle taxes or contact any broker outside the cTrader terminal. Verify tax treatment with your account provider.

## Build on GitHub from an iPad

1. Open the **Actions** tab of this repository. Click **Build Marius Gold AI DEMO** and open the newest run.
2. When the run shows green, download artifact \`MariusGoldAI-DEMO\` (ZIP) from the run page.
3. In iPad Files, unzip and find \`.algo\`. In cTrader Mobile use **Algo / cBots → Upload**, select the .algo and choose **DEMO account**, XAUUSD, M15.
4. Leave \`Enable DEMO orders=false\` for signal observation, then test demo orders separately only after inspecting logs and limits.
5. Start one cTrader cloud instance. Demo cloud instances stop after up to 7 days and must be restarted.
6. Review activity daily. A GitHub repository does **not** run a bot by itself.

If the GitHub build fails, **do not install or use** until errors are resolved. C# source code has not yet been empirically backtested in cTrader; cloud availability can vary by broker.

Official documentation:
- https://help.ctrader.com/ctrader-algo/documentation/visual-studio-ides/
- https://help.ctrader.com/ctrader-algo/documentation/cloud-features/
- https://help.ctrader.com/ctrader-algo/documentation/ui-basics/

## Future enhancements (not in v1)

Economic news filter, testing/optimisation report, statistical learning trained strictly on **historical demo data**, drawdown dashboard, broker fee/slippage modelling. These must be validated separately; never optimise on test data only.

**Safety:** Do not commit login details, passwords, tokens, or personal trading statements to this public repository.
