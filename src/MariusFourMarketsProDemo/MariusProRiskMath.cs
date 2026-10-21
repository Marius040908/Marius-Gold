using System;
namespace cAlgo.Robots
{
    // Pure mathematics shared between the trading algorithm and deterministic tests.
    public static class MariusProRiskMath
    {
        private static bool Ok(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        public static double TargetPips(double stopPips, double estimatedStopRisk,
                                        double equity, double targetPercent)
        {
            if (!Ok(stopPips) || !Ok(estimatedStopRisk) || !Ok(equity) || !Ok(targetPercent)
                || stopPips <= 0 || estimatedStopRisk <= 0 || equity <= 0 || targetPercent <= 0)
                return double.NaN;
            return stopPips * ((equity * targetPercent / 100.0) / estimatedStopRisk);
        }

        public static double NewPositionRiskBudget(double equity, double signalRiskPercent,
                                                    double learningMultiplier,
                                                    double existingStopRisk)
        {
            if (!Ok(equity) || !Ok(signalRiskPercent) || !Ok(learningMultiplier)
                || !Ok(existingStopRisk) || equity <= 0 || signalRiskPercent <= 0
                || learningMultiplier <= 0 || learningMultiplier > 1 || existingStopRisk < 0)
                return 0;
            // Hard trade maximum 1%, total open-stop maximum 2%.
            double single = equity * Math.Min(1.0, signalRiskPercent) / 100.0
                            * learningMultiplier;
            double available = Math.Max(0, equity * 0.02 - existingStopRisk);
            return Math.Min(single, available);
        }

        public static bool Defensive(int count, int needed, int losingPositions, double netPnl)
        {
            return needed >= 12 && count >= needed && losingPositions >= 0
                   && losingPositions * 3 >= needed * 2
                   && Ok(netPnl) && netPnl < 0;
        }
    }
}
