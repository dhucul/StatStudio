# StatStudio

A Minitab-style statistics workbench for Windows — a column **worksheet**, a Minitab-style
**Session** output pane, and analyses run from a `Stat`/`Graph` menu. Built with **.NET 10 +
WPF**, **Math.NET Numerics** (math) and **ScottPlot** (graphs).

## Features

- **Worksheet** — editable column grid (C1, C2, …); import/export **CSV/TSV** and **Excel (.xlsx)**;
  native **`.ssproj`** project files; missing values as `*`.
- **Basic Statistics** — descriptive statistics (Minitab quartile method), 1-sample / 2-sample
  (pooled & Welch) / paired *t*, 1- & 2-proportion, chi-square goodness-of-fit and association,
  Pearson/Spearman correlation, Anderson-Darling normality, F-test for two variances, Fisher's exact.
- **Nonparametrics** — Mann-Whitney, Wilcoxon signed-rank, Kruskal-Wallis, sign test, runs test.
- **ANOVA** — one-way (with Tukey HSD post-hoc), two-way (with interaction), tests for equal variances (Bartlett, Levene).
- **Regression** — simple, multiple, polynomial, best-subsets (Mallows Cp), stepwise, and binary
  logistic; coefficient SE/t/p, R²/adj-R²/S, ANOVA table, fitted-line and residual plots.
- **Time series** — trend analysis, moving average, single/double/Winters exponential smoothing,
  classical decomposition, ACF/PACF, **ARIMA(p,d,q)** (forecasts with confidence bands);
  MAPE/MAD/MSD accuracy and actual/fitted/forecast plots.
- **Multivariate** — principal components (scree plot), k-means clustering.
- **DOE** — create full 2^k and 2^(k-p) fractional factorial designs (generators, resolution,
  defining relation, replicates, center points, randomized run order); analyze 2-level
  factorials (effects, coefficients, ANOVA, Pareto of effects).
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
dotnet run  --project tools/StatStudio.Smoke      # 225 numeric checks vs reference values
```

`StatStudio.Wpf --demo` loads a sample dataset and runs a few analyses (see `ProcessArgs` for the
`--shot-*` screenshot flags used during development).

## Package

```
powershell -ExecutionPolicy Bypass -File installer/build-installer.ps1   # -> dist/StatStudioSetup.exe
powershell -ExecutionPolicy Bypass -File tools/e2e-install.ps1           # install -> launch -> uninstall
```

Self-contained win-x64 (no .NET prerequisite); installs **per-user** (no UAC).

## Not yet included

Response-surface/mixture designs, seasonal ARIMA (SARIMA), factor analysis, reliability/survival,
and a worksheet calculator language. The engine-per-analysis + dialog + Session-output design
makes these additive.
