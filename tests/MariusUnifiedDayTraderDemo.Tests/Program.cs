using System;
using cAlgo.Robots;
class Program
{
    static void Ok(bool b, string name) { if (!b) throw new Exception("FAIL "+name); Console.WriteLine("PASS "+name); }
    static bool Eq(double a, double b) => Math.Abs(a-b) < 1e-8;
    static void Main()
    {
        Ok(Eq(MariusUnifiedRules.TargetPips(40),120),"target always 3R");
        Ok(Eq(MariusUnifiedRules.TargetPips(1.5),4.5),"fractional pips");
        Ok(double.IsNaN(MariusUnifiedRules.TargetPips(0)),"zero stop rejected");
        Ok(double.IsNaN(MariusUnifiedRules.TargetPips(-1)),"negative stop rejected");
        Ok(Eq(MariusUnifiedRules.RemainingDailyBudget(100,0,0),3),"100 demo max daily risk 3");
        Ok(Eq(MariusUnifiedRules.RemainingDailyBudget(100,1,1),1),"losses plus positions subtract");
        Ok(Eq(MariusUnifiedRules.RemainingDailyBudget(100,5,0),0),"exhausted loss budget");
        Ok(Eq(MariusUnifiedRules.RemainingDailyBudget(-100,0,0),0),"negative balance");
        Ok(Eq(MariusUnifiedRules.RemainingDailyBudget(100,-2,0),0),"invalid losses");
        Ok(MariusUnifiedRules.WithinEntryLimit(0),"allow first");
        Ok(MariusUnifiedRules.WithinEntryLimit(3),"allow fourth");
        Ok(!MariusUnifiedRules.WithinEntryLimit(4),"reject fifth");
        Ok(!MariusUnifiedRules.WithinEntryLimit(-1),"invalid count");
        Ok(MariusUnifiedRules.EntryRiskAllowed(0.8,1),"risk within limit");
        Ok(!MariusUnifiedRules.EntryRiskAllowed(1.2,1),"risk above limit");
        Ok(!MariusUnifiedRules.EntryRiskAllowed(0,1),"zero risk rejected");
        Console.WriteLine("16 UNIT CHECKS PASSED; these do not validate profitability.");
    }
}
