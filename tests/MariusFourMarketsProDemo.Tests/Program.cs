using System;
using cAlgo.Robots;
class Program
{
    static void Assert(bool condition, string name)
    {
        if(!condition) throw new Exception("FAILED: " + name);
        Console.WriteLine("PASS: " + name);
    }
    static bool Eq(double x, double y) => Math.Abs(x-y) < 1e-9;
    static void Main()
    {
        // At equity 2,000, 1% risk=20 and 3% target=60;
        // if the actual lot-size risk is only 5, the TP must be 12R, not 3R.
        Assert(Eq(MariusProRiskMath.TargetPips(100, 20, 2000, 3), 300), "3% target with 1% actual risk");
        Assert(Eq(MariusProRiskMath.TargetPips(100, 5, 2000, 3), 1200), "3% target with 0.25% actual risk");
        Assert(double.IsNaN(MariusProRiskMath.TargetPips(100, 0, 2000, 3)), "invalid zero risk rejected");
        Assert(double.IsNaN(MariusProRiskMath.TargetPips(100, 20, -1, 3)), "invalid account equity rejected");
        Assert(Eq(MariusProRiskMath.NewPositionRiskBudget(2000, 1.0, 1.0, 0),20), "never exceed 1% per position");
        Assert(Eq(MariusProRiskMath.NewPositionRiskBudget(2000, 0.5, 1.0, 0),10), "weaker signal uses smaller risk");
        Assert(Eq(MariusProRiskMath.NewPositionRiskBudget(2000, 1.0, 0.5, 0),10), "defensive mode halves trade risk");
        Assert(Eq(MariusProRiskMath.NewPositionRiskBudget(2000, 1.0, 1.0, 30),10), "combined 2% stop risk enforced");
        Assert(Eq(MariusProRiskMath.NewPositionRiskBudget(2000, 1.0, 1.0, 45),0), "full exposure blocks entries");
        Assert(Eq(MariusProRiskMath.NewPositionRiskBudget(2000, 3.0, 1.0, 0),20), "over-1% signal capped");
        Assert(!MariusProRiskMath.Defensive(11,12,10,-100), "not defensive until 12 outcomes");
        Assert(!MariusProRiskMath.Defensive(12,12,7,-100), "not defensive under eight losses");
        Assert(!MariusProRiskMath.Defensive(12,12,8,10), "not defensive if profitable");
        Assert(MariusProRiskMath.Defensive(12,12,8,-1), "defensive after eight losing positions");
        Console.WriteLine("ALL 14 DETERMINISTIC TESTS PASSED.");
    }
}
