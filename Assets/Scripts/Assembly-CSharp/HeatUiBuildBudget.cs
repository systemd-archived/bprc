using System;
using System.Diagnostics;
using UnityEngine;

// All settings pages share one budget, instead of each spending a separate slice in the same frame.
public static class HeatUiBuildBudget
{
    private const double BudgetMilliseconds = 3.0;
    private static int s_frame = -1;
    private static double s_spent;
    private static readonly Stopwatch s_clock = new Stopwatch();

    public static bool TryRun(Action action)
    {
        if (s_frame != Time.frameCount)
        {
            s_frame = Time.frameCount;
            s_spent = 0;
        }
        if (s_spent >= BudgetMilliseconds) return false;
        s_clock.Restart();
        try { action(); }
        finally { s_spent += s_clock.Elapsed.TotalMilliseconds; }
        return true;
    }
}
