# StatStudio

A Minitab-style statistics workbench for Windows — a column **worksheet**, a Minitab-style
**Session** output pane, and analyses run from a `Stat`/`Graph` menu. Built with **.NET 10 +
WPF**, **Math.NET Numerics** (math) and **ScottPlot** (graphs).

## Features

- **Worksheet** — editable column grid (C1, C2, …); import/export **CSV/TSV** and **Excel (.xlsx)**;
  native **`.ssproj`** project files; missing values as `*`. **File ▸ Sample Data** loads
  ten built-in example datasets tuned to the various analyses.
- **Basic Statistics** — descriptive statistics (Minitab quartile method), 1-sample / 2-sample
  (pooled & Welch) / paired *t*, 1- & 2-proportion, chi-square goodness-of-fit and association,
  Pearson/Spearman correlation, Anderson-Darling normality, F-test for two variances, Fisher's exact.
- **Nonparametrics** — Mann-Whitney, Wilcoxon signed-rank, Kruskal-Wallis, sign test, runs test.
- **ANOVA** — one-way (with Tukey HSD post-hoc), two-way (with interaction), tests for equal variances (Bartlett, Levene).
- **Regression** — simple, multiple, polynomial, best-subsets (Mallows Cp), stepwise, and binary
  logistic; coefficient SE/t/p, R²/adj-R²/S, ANOVA table, fitted-line and residual plots.
- **Time series** — trend analysis, moving average, single/double/Winters exponential smoothing,
  classical decomposition, ACF/PACF, **ARIMA(p,d,q)** and **seasonal SARIMA(p,d,q)(P,D,Q)ₛ**
  (forecasts with confidence bands); MAPE/MAD/MSD accuracy and actual/fitted/forecast plots.
- **Multivariate** — principal components (scree plot), factor analysis (varimax rotation),
  k-means clustering.
- **Bayesian** — Beta-Binomial proportion, 1-sample normal mean (known & unknown variance),
  and reference-prior linear regression: posterior summaries, credible intervals, tail probabilities.
- **Mixed / hierarchical** — one-way random-effects (random-intercept) model: variance components,
  intraclass correlation, and shrinkage (BLUP) group estimates.
- **Reliability / survival** — parametric life-data fitting (Weibull/exponential/lognormal/normal
  MLE + Weibull probability plot) and Kaplan-Meier survival. Both accept an optional 0/1
  **right-censoring** column; censored units contribute survival time to the likelihood, and the
  probability plot uses Johnson rank-adjusted plotting positions. Parameters, percentiles and the
  mean/StDev/median all carry **standard errors and confidence intervals** — observed Fisher
  information for the parameters, the delta method for functions of them.
- **DOE** — create full 2^k / 2^(k-p) fractional factorial designs (generators, resolution,
  defining relation), **response-surface** designs (central composite + Box-Behnken), and
  **mixture** designs (simplex-lattice + simplex-centroid); analyze 2-level factorials
  (effects, Pareto), second-order response surfaces, and Scheffé mixture models.
- **Calculator** — compute a new column from a formula: `Cn`/named columns, `+ - * / ^`,
  per-row functions (sqrt, log, exp, trig, round) and aggregates (mean, sum, stdev, …).
- **Control charts (SPC)** — Xbar-R, Xbar-S, I-MR, P, NP, C, U (with Nelson tests 1 & 2).
- **Quality tools** — normal process capability (Cp, Cpk, Pp, Ppk, Cpm); crossed **Gage R&R**
  (ANOVA method: variance components, %study var, distinct categories); power & sample-size
  calculators (1-/2-sample t, 1-proportion).
- **Graphs** — histogram, boxplot, scatterplot, time-series plot, normal probability plot.

## Layout

```
StatStudio.slnx
  src/StatStudio.Core   (net10.0)          pure, UI-free engines — Data/, Statistics/, Statistics/Spc/, Inference/
  src/StatStudio.Wpf    (net10.0-windows)  WPF UI, dialogs, ScottPlot graphs (x64)
  tools/StatStudio.Smoke(net10.0)          deterministic numeric self-tests
  assets/               make-icon.ps1, capture.ps1
  installer/            StatStudio.iss, build-installer.ps1
```

The editable `DataTable` is the live worksheet; analyses use a detached Core `Worksheet`
snapshot after committing edits. Declared column types survive this conversion. Numeric results
are stored at full precision and rounded only for display. Column-header sorting is disabled so
the visible worksheet, ordered analyses, and exports keep the same observation order.

Imports let you choose whether the first row contains column names. For a CSV exported by
StatStudio, select the export option to decode its reversible formula guards; leave it off for
external CSVs to preserve literal apostrophes. XLSX uses text/quote-prefix metadata instead.
Use `.ssproj` to preserve declared text identifiers such as `00123` and other column types.

Time-series, I-MR, and capability inputs reject missing observations before the final value;
trailing empty worksheet padding is ignored. Moving averages use trailing windows for both odd
and even lengths. Power calculations honor signed alternatives and return whole sample sizes.

Heavy analyses and file operations run in the background with a Cancel operation button.
Numerical loops check cancellation; third-party file/matrix calls finish their current call
before cancellation can be observed. Limits are 10,000 forecasts/design runs, 512 worksheet
columns, 2,000,000 worksheet cells, and 64 MB per imported file.

## Build / run / test

```
dotnet build StatStudio.slnx
dotnet run  --project src/StatStudio.Wpf
dotnet test StatStudio.slnx
dotnet run  --project tools/StatStudio.Smoke      # optional verbose smoke output
```

`StatStudio.Wpf --demo` loads a sample dataset and runs a few analyses (see `ProcessArgs` for the
`--shot-*` screenshot flags used during development).

## Package

```
powershell -ExecutionPolicy Bypass -File installer/build-installer.ps1   # -> dist/StatStudioSetup.exe
powershell -ExecutionPolicy Bypass -File tools/e2e-install.ps1 -InstallDir C:\temp\StatStudio-e2e
```

Self-contained win-x64 (no .NET prerequisite); installs machine-wide under **Program Files**
and requests administrator approval.

## Scope

StatStudio covers Minitab's mainstream surface and beyond: basic statistics, nonparametrics,
ANOVA/regression (incl. logistic), the full DOE suite (factorial, fractional, response-surface,
mixture), SPC + capability + Gage R&R, the full time-series suite (smoothing, decomposition,
ARIMA/SARIMA), multivariate (PCA, factor analysis, clustering), reliability/survival, power &
sample size, conjugate Bayesian inference, one-way mixed/hierarchical models, and a worksheet
calculator — all engine-verified by the discoverable smoke test suite. The engine-per-analysis + dialog +
Session-output design keeps further additions incremental.
