namespace StatStudio.Core.Statistics;

public sealed record SubsetModel(
    IReadOnlyList<string> Predictors, int NumPredictors,
    double RSquared, double RSquaredAdj, double S, double MallowsCp);

public sealed record BestSubsetsResult(IReadOnlyList<SubsetModel> Models);

public sealed record StepwiseResult(RegressionResult Final, IReadOnlyList<string> Steps);

public static class RegressionExtensions
{
    /// <summary>Polynomial regression of y on x of the given degree (predictors x, x², …).</summary>
    public static RegressionResult Polynomial(double[] x, double[] y, int degree, string xName = "X", string response = "Y", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (degree < 1) throw new ArgumentException("Degree must be >= 1.");
        var preds = new double[degree][];
        var names = new string[degree];
        for (int d = 1; d <= degree; d++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            preds[d - 1] = x.Select(v => Math.Pow(v, d)).ToArray();
            names[d - 1] = d == 1 ? xName : $"{xName}^{d}";
        }
        return Regression.Fit(y, preds, names, response);
    }

    /// <summary>All-subsets regression: every non-empty predictor subset, ranked by adjusted R².</summary>
    public static BestSubsetsResult BestSubsets(double[] y, double[][] predictors, IReadOnlyList<string> names, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int k = predictors.Length;
        if (k > 12) throw new ArgumentException("Best subsets is limited to 12 predictors.");

        // Only Mallows Cp needs the full model. A collinear full set is exactly the situation
        // best-subsets is run to diagnose, so its failure must not abort the whole search.
        double mseFull;
        try { mseFull = Regression.Fit(y, predictors, names).MsError; }
        catch (ArgumentException) { mseFull = double.NaN; }
        int n = y.Length;

        var models = new List<SubsetModel>();
        for (int mask = 1; mask < (1 << k); mask++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var idx = Enumerable.Range(0, k).Where(b => (mask & (1 << b)) != 0).ToArray();
            var subPreds = idx.Select(b => predictors[b]).ToArray();
            var subNames = idx.Select(b => names[b]).ToList();
            try
            {
                var r = Regression.Fit(y, subPreds, subNames);
                int pParams = idx.Length + 1;
                double cp = mseFull > 0 ? r.SsError / mseFull - n + 2.0 * pParams : double.NaN;
                models.Add(new SubsetModel(subNames, idx.Length, r.RSquared, r.RSquaredAdj, r.S, cp));
            }
            catch (ArgumentException) { /* singular subset — skip */ }
        }
        return new BestSubsetsResult(models.OrderByDescending(m => m.RSquaredAdj).ToList());
    }

    /// <summary>Forward stepwise selection by p-value (enter while min p &lt; alphaEnter).</summary>
    public static StepwiseResult Stepwise(double[] y, double[][] predictors, IReadOnlyList<string> names,
        double alphaEnter = 0.15, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StatGuard.Design(y, predictors, names);
        if (predictors.Length == 0)
            throw new ArgumentException("Stepwise selection needs at least one predictor.", nameof(predictors));
        StatGuard.Probability(alphaEnter, nameof(alphaEnter));
        int k = predictors.Length;
        var inModel = new List<int>();
        var steps = new List<string>();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int best = -1;
            double bestP = double.PositiveInfinity;
            foreach (var cand in Enumerable.Range(0, k).Where(i => !inModel.Contains(i)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var idx = inModel.Append(cand).ToArray();
                try
                {
                    var r = Regression.Fit(y, idx.Select(b => predictors[b]).ToArray(), idx.Select(b => names[b]).ToList());
                    double pNew = r.Terms.Last().P;  // candidate is the last predictor added
                    if (pNew < bestP) { bestP = pNew; best = cand; }
                }
                catch (ArgumentException) { /* skip singular */ }
            }
            if (best < 0 || bestP >= alphaEnter) break;
            inModel.Add(best);
            steps.Add($"Step {inModel.Count}: + {names[best]}  (p = {bestP:0.000})");
        }

        if (inModel.Count == 0)
        {
            int fallback = EnumerableArgMin(y, predictors, names, cancellationToken);
            if (fallback < 0)
                throw new ArgumentException("No nonsingular one-predictor model can be fit.", nameof(predictors));
            // A model is still returned so the caller has something to report, but say plainly
            // that it failed the criterion — otherwise the coefficient table reads as a result.
            steps.Add($"No predictor met the entry criterion (alpha to enter = {alphaEnter:0.###}).");
            steps.Add($"Showing the best single-predictor model ('{names[fallback]}') for reference only — " +
                      "it did NOT meet the entry criterion and should not be interpreted as a selected model.");
            inModel.Add(fallback);
        }

        var finalIdx = inModel.ToArray();
        var final = Regression.Fit(y, finalIdx.Select(b => predictors[b]).ToArray(), finalIdx.Select(b => names[b]).ToList());
        return new StepwiseResult(final, steps);
    }

    private static int EnumerableArgMin(double[] y, double[][] predictors, IReadOnlyList<string> names, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // fallback: the single predictor with the best (lowest) p-value
        int best = -1; double bestP = double.PositiveInfinity;
        for (int i = 0; i < predictors.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var r = Regression.Fit(y, new[] { predictors[i] }, new[] { names[i] });
                if (r.Terms[1].P < bestP) { bestP = r.Terms[1].P; best = i; }
            }
            catch (ArgumentException) { }
        }
        return best;
    }
}
