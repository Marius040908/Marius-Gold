using System;
namespace MariusFourMarketsStrategy
{
    public sealed class MarketProfile
    {
        public string Name { get; }
        public double SpreadAtrMaxPercent { get; }
        public double MinTrendGapAtr { get; }
        public double MinBodyAtr { get; }
        public double BuyRsiMin { get; }
        public double BuyRsiMax { get; }
        public double SellRsiMin { get; }
        public double SellRsiMax { get; }
        public double BaseRiskPercent { get; }
        public MarketProfile(string name, double spread, double trendGap, double body,
                             double buyMin, double buyMax, double sellMin, double sellMax,
                             double baseRisk)
        {
            Name=name; SpreadAtrMaxPercent=spread; MinTrendGapAtr=trendGap;
            MinBodyAtr=body; BuyRsiMin=buyMin; BuyRsiMax=buyMax;
            SellRsiMin=sellMin; SellRsiMax=sellMax; BaseRiskPercent=baseRisk;
        }
    }
    public static class Profiles
    {
        // Purposefully different for metal vs crypto: not empirically optimized.
        private static readonly MarketProfile[] All = {
            new MarketProfile("GOLD", 12.0, 0.13, 0.09, 55, 70, 30, 45, 0.50),
            new MarketProfile("SILVER", 10.0, 0.20, 0.12, 57, 69, 31, 43, 0.50),
            new MarketProfile("BITCOIN", 15.0, 0.16, 0.13, 56, 70, 30, 44, 0.50),
            new MarketProfile("ETHEREUM", 14.0, 0.19, 0.15, 58, 70, 30, 42, 0.50)
        };
        public static MarketProfile ForKind(int kind)
        {
            if(kind<0||kind>=All.Length) throw new ArgumentOutOfRangeException(nameof(kind));
            return All[kind];
        }
    }
    public static class Rules
    {
        public static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        public static bool GoodMarket(double spreadPercent, double trendGapAtr, double candleBodyAtr,
                                      double maxSpread, double minTrendGap, double minBody)
        {
            return Finite(spreadPercent) && Finite(trendGapAtr) && Finite(candleBodyAtr)
                && spreadPercent>=0 && spreadPercent<=maxSpread
                && trendGapAtr>=minTrendGap && candleBodyAtr>=minBody;
        }
        public static double RiskPercent(int confirmations, double baseRisk, double cap)
        {
            if(!Finite(baseRisk)||!Finite(cap)||baseRisk<=0||cap<=0) return 0;
            // 0.50 / 0.65 / 0.80 / 0.95% max on strongest confirmations;
            // can be further halved in defensive learning mode.
            return Math.Min(cap, baseRisk + Math.Min(Math.Max(confirmations,0),3)*0.15);
        }
        public static double TakeProfitPips(double stopPips, double actualStopRisk, double targetAccountProfit)
        {
            if(!Finite(stopPips)||!Finite(actualStopRisk)||!Finite(targetAccountProfit)
                 ||stopPips<=0||actualStopRisk<=0||targetAccountProfit<=0)
                return double.NaN;
            return stopPips*(targetAccountProfit/actualStopRisk);
        }
        public static bool Defensive(int sampleCount, int requiredCount, int losers, double totalNet)
        {
            return sampleCount>=requiredCount && requiredCount>=12 && losers*3>=requiredCount*2
                   && Finite(totalNet) && totalNet<0;
        }
    }
}
