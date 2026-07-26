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
  MLE with percentiles + Weibull probability plot) and Kaplan-Meier survival (right-censoring).
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

The Core worksheet is the single source of truth for analyses; the WPF grid edits a `DataTable`
that is converted to a Core `Worksheet` whenever an analysis runs.

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

Self-contained win-x64 (no .NET prerequisite); installs **per-user** (no UAC).

## Scope

StatStudio covers Minitab's mainstream surface and beyond: basic statistics, nonparametrics,
ANOVA/regression (incl. logistic), the full DOE suite (factorial, fractional, response-surface,
mixture), SPC + capability + Gage R&R, the full time-series suite (smoothing, decomposition,
ARIMA/SARIMA), multivariate (PCA, factor analysis, clustering), reliability/survival, power &
sample size, conjugate Bayesian inference, one-way mixed/hierarchical models, and a worksheet
calculator — all engine-verified by the discoverable smoke test suite. The engine-per-analysis + dialog +
Session-output design keeps further additions incremental.
