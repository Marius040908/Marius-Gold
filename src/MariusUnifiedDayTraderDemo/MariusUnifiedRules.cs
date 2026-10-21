using System;
namespace cAlgo.Robots
{
    // Pure functions for isolated unit tests. Risk is nominal and never a guaranteed loss ceiling.
    public static class MariusUnifiedRules
    {
        public static double TargetPips(double stopPips)
        {
            return double.IsNaN(stopPips) || double.IsInfinity(stopPips) || stopPips <= 0
                ? double.NaN : stopPips * 3.0;
        }
        public static double RemainingDailyBudget(double startBalance, double realizedLosses, double openStopRisk)
        {
            if (double.IsNaN(startBalance) || double.IsInfinity(startBalance) || startBalance <= 0
                || double.IsNaN(realizedLosses) || double.IsInfinity(realizedLosses) || realizedLosses < 0
                || double.IsNaN(openStopRisk) || double.IsInfinity(openStopRisk) || openStopRisk < 0)
                return 0;
            return Math.Max(0, startBalance * 0.03 - realizedLosses - openStopRisk);
        }
        public static bool WithinEntryLimit(int entriesToday) => entriesToday >= 0 && entriesToday < 4;
        public static bool EntryRiskAllowed(double proposedRisk, double remaining)
        {
            return !double.IsNaN(proposedRisk) && !double.IsInfinity(proposedRisk)
                && proposedRisk > 0 && proposedRisk <= remaining + 1e-8;
        }
    }
}
