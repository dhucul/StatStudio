using StatStudio.Core.Inference;

namespace StatStudio.Core.Statistics.Spc;

public static class SpcFormatter
{
    public static string Chart(SpcChart c)
    {
        bool constLimits = c.Ucl.Distinct().Count() == 1 && c.Lcl.Distinct().Count() == 1;
        var sb = new System.Text.StringBuilder();
        sb.Append(c.Title).Append('\n');
        sb.Append($"  Center (CL) = {Fmt.N(c.Center, 4)}\n");
        if (constLimits)
        {
            sb.Append($"  UCL = {Fmt.N(c.Ucl[0], 4)}\n");
            sb.Append($"  LCL = {Fmt.N(c.Lcl[0], 4)}\n");
        }
        else
        {
            sb.Append("  UCL/LCL vary by subgroup size.\n");
        }
        sb.Append($"  Points = {c.Values.Length}\n");

        var ooc = new List<int>();
        for (int i = 0; i < c.OutOfControl.Length; i++) if (c.OutOfControl[i]) ooc.Add(i);
        if (ooc.Count == 0)
            sb.Append("  No points failed the control tests.");
        else
        {
            sb.Append($"  {ooc.Count} point(s) out of control:\n");
            var t = new TextTable("Point", "Value", "Test(s)").LeftAlign(2);
            foreach (var i in ooc) t.Add((i + 1).ToString(), Fmt.N(c.Values[i], 4), c.Signals[i]);
            sb.Append(t);
        }
        return sb.ToString();
    }

    public static string Pair(string heading, SpcChart a, SpcChart b) =>
        $"{heading}\n\n{Chart(a)}\n\n{Chart(b)}";

    public static string Capability(CapabilityResult r)
    {
        var proc = new TextTable("N", "Mean", "StDev (Within)", "StDev (Overall)");
        proc.Add(r.N.ToString(), Fmt.N(r.Mean, 4), Fmt.N(r.SigmaWithin, 4), Fmt.N(r.SigmaOverall, 4));

        string spec = $"LSL = {(r.Lsl.HasValue ? Fmt.G(r.Lsl.Value) : "*")}, " +
                      $"USL = {(r.Usl.HasValue ? Fmt.G(r.Usl.Value) : "*")}" +
                      (r.Target.HasValue ? $", Target = {Fmt.G(r.Target.Value)}" : "");

        var within = new TextTable("Cp", "Cpk").Add2(Fmt.N(r.Cp), Fmt.N(r.Cpk));
        var overall = new TextTable("Pp", "Ppk", "Cpm").Add3(Fmt.N(r.Pp), Fmt.N(r.Ppk),
            double.IsNaN(r.Cpm) ? "*" : Fmt.N(r.Cpm));

        return "Process Capability (Normal)\n\n" +
               "Process Data\n" + proc + "\n" + "  " + spec + "\n\n" +
               "Potential (Within) Capability\n" + within + "\n\n" +
               "Overall Capability\n" + overall;
    }

    private static TextTable Add2(this TextTable t, string a, string b) { t.Add(a, b); return t; }
    private static TextTable Add3(this TextTable t, string a, string b, string c) { t.Add(a, b, c); return t; }
}
