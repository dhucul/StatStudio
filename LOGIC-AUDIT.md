# StatStudio — whole-application logic audit

Audited the current application source as a cohesive system, including WPF entry points and dialogs, worksheet conversion, persistence, calculator, statistical engines, output formatting, graphs, and installer scripts. No git diff or change history was used. Application source was not modified.

The existing Release smoke runner completed with **425 passed, 0 failed**. Supplemental isolated probes exercised the current compiled Core library, the actual WorksheetGrid source, and WPF checkbox thread ownership. Findings marked **reproduced** have those additional runtime checks; other findings follow from static execution tracing. This is not a formal proof of every possible numeric input, a visual UI acceptance test, or a certification of the statistical methods. Installer actions were inspected, not executed.

**Execution sequence**

1. `App.OnStartup` installs exception handlers. WPF creates MainWindow through StartupUri. MainWindow initializes controls, populates sample-data actions, creates the initial DataTable, and logs readiness. Startup arguments run after Loaded, which correctly permits owned graph windows.
2. The editable DataTable is the live worksheet state. Column/row events set `_dirty`. Analysis and save commands commit the current row and create a fresh Core Worksheet snapshot. Most analysis dialogs then run modally against that snapshot.
3. Inputs are extracted from the snapshot, analyzed, formatted, and appended to Session. Graphs are constructed after the calculations. Most calculations, imports, and exports execute synchronously on the UI thread; ARIMA and SARIMA are the two background-fit handlers.
4. The background handlers disable the window, create a cancellation source, dispatch a worker task, await it, check cancellation/window lifetime, publish results, and re-enable the window. Findings 1 and 2 break this lifecycle.
5. Save operations write a temporary sibling file, close the writer, move the file to the destination, and only then mark the worksheet clean. This ordering is sound. Calculator evaluation also completes before the destination column is cleared, protecting self-referential expressions from reading partially overwritten data.
6. Replacing or closing a dirty worksheet invokes a discard confirmation. Closing also cancels the stored fit source. There is no rollback mechanism in the global exception handler: marking an exception handled does not undo prior state changes.

**High-priority defects**

**1. [P1] Both background-fit handlers read a WPF control from the worker thread — reproduced.**

Locations: [MainWindow.xaml.cs:1342](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/MainWindow.xaml.cs:1342), [MainWindow.xaml.cs:1371](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/MainWindow.xaml.cs:1371), [ArimaWindow.xaml.cs:12](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/Dialogs/ArimaWindow.xaml.cs:12), [SarimaWindow.xaml.cs:16](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/Dialogs/SarimaWindow.xaml.cs:16).

The worker lambda evaluates `dlg.IncludeConstant`, whose getter reads `ConstBox.IsChecked`. This is a dependency property on a UI-thread-owned control. The read throws `InvalidOperationException` before `Fit` is invoked. Normal menu-driven ARIMA and SARIMA attempts therefore fail even when every numeric option is valid. The other order properties are ordinary stored values; the checkbox getter is the offending dependency. Capture all options into plain values on the UI thread before calling Task.Run.

**2. [P1] A disposed cancellation source remains stored, and a subsequent failure leaves the window disabled — reproduced primitives; full handler sequence statically traced.**

Locations: [MainWindow.xaml.cs:336](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/MainWindow.xaml.cs:336), [MainWindow.xaml.cs:347](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/MainWindow.xaml.cs:347), [MainWindow.xaml.cs:1337](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/MainWindow.xaml.cs:1337).

`using var cts` disposes the source when a fit handler exits, including after finding 1, but `_fitCancellation` continues to reference it. The next `StartFit()` calls Cancel on that disposed source and throws ObjectDisposedException. Crucially, this happens after `IsEnabled = false` and before entering the try/finally, so re-enabling is skipped. Closing after a completed attempt also calls Cancel on the disposed object. Put the full state transition inside try/finally, and clear the stored reference when releasing the source that owns it.

**3. [P1] Fractional-factorial analysis counts the intercept as an effect and inflates R² — reproduced.**

Location: [FactorialAnalysis.cs:80](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/FactorialAnalysis.cs:80).

The alias dictionary starts empty even though a Constant term already exists. In the generated fraction `C=AB`, the `A*B*C` contrast is all +1. It is treated as a new effect, assigned the response mean, and added to model sum of squares. For `Y=10+2A+3B`, the result includes both Constant=10 and A*B*C=10, with R²=8.6923076923, displayed as **869.23%**. Opposite-sign aliases are also not recognized because signatures only match exactly. Handle constant and signed aliases explicitly, and derive fit statistics from a valid independent model or fitted residuals.

**4. [P1] The normality p-value approximation reverses its conclusion for sufficiently large AD statistics — reproduced.**

Locations: [Normality.cs:46](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/Normality.cs:46), [NonparametricFormatters.cs:84](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/NonparametricFormatters.cs:84).

The large-statistic branch uses `exp(1.2937 - 5.709*a + 0.0186*a*a)` without restricting its approximation range. The positive quadratic term eventually dominates. A sample of 1,000 zeros and 1,000 ones produces AD≈359.1174 and p≈2.427e152. The formatter treats that impossible probability as evidence to “fail to reject normality.” Use a valid large-statistic tail treatment and reject invalid probabilities before assigning a verdict; merely clamping this erroneous result to 1 would preserve the wrong conclusion.

**5. [P1] Installer cleanup is not restricted to files owned by StatStudio — static.**

Location: [StatStudio.iss:54](C:/Users/dhucu/source/repos/StatStudio/installer/StatStudio.iss:54).

Before copying its payload, the installer deletes every `.exe`, `.dll`, `.json`, and `.pdb` in the selected application directory, plus the entire `runtimes` subdirectory. Directory selection is enabled. If the destination is a shared existing folder, unrelated files matching these patterns are removed. The comments claim ownership scoping, but extensions do not establish ownership. Use an explicit previous-payload manifest and validate the application destination before cleanup.

**Data and state defects**

**6. [P2] Mixture creation rounds values beyond the analyzer's tolerance — reproduced.**

Locations: [MainWindow.xaml.cs:1113](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/MainWindow.xaml.cs:1113), [MixtureAnalysis.cs:28](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/MixtureAnalysis.cs:28), [SampleData.cs:171](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Data/SampleData.cs:171).

Creation stores component proportions with five decimals, but analysis requires row sums within 1e-8 of 1. A generated three-component centroid becomes 0.33333+0.33333+0.33333=0.99999, and analysis rejects run 7. The built-in Concrete Mixture sample stores thirds with four decimals and has the same inconsistency. Preserve round-trip precision in worksheet values and reserve rounding for presentation.

**7. [P2] Header inference silently drops valid observations and fails on empty saved worksheets — reproduced.**

Location: [WorksheetIo.cs:187](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Data/WorksheetIo.cs:187).

The missing marker `*` qualifies as a nonnumeric heading. Importing the headerless data `*,2 / 3,4 / 5,6` returns only two observations and columns named `*` and `2`. Separately, `rows.Count < 2` always selects “no header”: exporting an empty column named Height and reimporting it creates C1 with a text observation Height. All-text headerless input is also ambiguous under the default heuristic. Exclude missing markers from header evidence and expose a header choice/preview in the import flow; the Core override currently has no UI equivalent.

**8. [P2] Formula-guard encoding is not reversible for every value or header — reproduced.**

Locations: [WorksheetIo.cs:34](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Data/WorksheetIo.cs:34), [WorksheetIo.cs:170](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Data/WorksheetIo.cs:170).

A legitimate text value `'=literal` is written unchanged, but the reader strips its original apostrophe and returns `=literal`. Conversely, a column name `=literal` is guarded on export and reimports as `'=literal`, because headers do not go through the decoder. The same data decoder is applied to XLSX even though that writer uses quote-prefix metadata. Make escaping unambiguous, distinguish CSV encoding from XLSX metadata, and treat header round trips consistently.

**9. [P2] Visible row sorting and analysis/export order diverge — reproduced.**

Locations: [WorksheetGrid.cs:46](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/WorksheetGrid.cs:46), [MainWindow.xaml:184](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/MainWindow.xaml:184).

The grid displays DefaultView and permits sorting, but snapshot conversion iterates the underlying `table.Rows`. For values 2,1 sorted ascending, the view begins with 1 while the analysis snapshot still begins with 2. Time-series calculations, moving ranges, runs tests, calculator row positions, and export can therefore use an order different from the worksheet's visible order. Establish explicit worksheet ordering semantics; either commit sorting to data order, extract the visible order, or make view-only sorting unmistakable for order-sensitive operations.

**10. [P2] Removing missing observations changes the time axis before time-series analysis — static.**

Locations: [MainWindow.xaml.cs:1234](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/MainWindow.xaml.cs:1234), [DataColumn.cs:59](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Data/DataColumn.cs:59).

OpenSeries and both ARIMA handlers use NumericValues, which removes missing cells and renumbers the remaining observations implicitly. January=10, February=*, March=30 becomes adjacent values 10,30. Seasonal alignment, differencing, autocorrelation, trend fitting, and reported forecast periods now describe a compressed time axis without notification. Retain positions with an explicit missing-data policy or reject series containing interior gaps before fitting. The related SPC paths also lose original worksheet row identifiers when filtering rows.

**11. [P2] Calculator output is rounded before becoming stored worksheet state — reproduced.**

Location: [MainWindow.xaml.cs:1086](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/MainWindow.xaml.cs:1086).

The calculator stores values using ten fixed decimal places. Even an identity expression applied to `0.00000000001` stores `0`, so subsequent calculations and exports use altered data. Only NaN is converted to missing: infinity from division by zero is written as text, causing the resulting column to fail LooksNumeric and disappear from numeric pickers. Store round-trip numeric text and define a consistent policy for all nonfinite results.

**12. [P2] The grid round trip loses declared column types — reproduced.**

Locations: [WorksheetGrid.cs:15](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/WorksheetGrid.cs:15), [WorksheetGrid.cs:70](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/WorksheetGrid.cs:70).

ToDataTable stores only names and strings; ToWorksheet re-infers Numeric or Text. Loading a project containing a declared Text ID column with `00123` changes its type to Numeric on the next snapshot, even without an edit. Saving to XLSX then writes the numeric value 123, losing the identifier's leading zeros. DateTime declarations also cannot survive. Preserve column metadata through the grid conversion and separate declared type from numeric-content inference.

**13. [P2] Calculator parser depth protection does not protect evaluation depth — static.**

Location: [Calculator.cs:93](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Data/Calculator.cs:93).

MaxDepth bounds recursive parsing of parentheses, unary operators, and powers. A long flat addition or multiplication chain is parsed by a loop, however, and creates a nested chain of delegates. Evaluating the first row recursively invokes every previous delegate. Sufficiently long flat input can therefore cause an uncatchable StackOverflowException despite the advertised depth guard. Bound expression/node depth as well, or evaluate an instruction sequence iteratively. This process-termination case was not executed against the application.

**Statistical branching and result defects**

**14. [P2] A power-of-two number of corners does not prove a regular fractional design — reproduced.**

Location: [FactorialAnalysis.cs:55](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/FactorialAnalysis.cs:55).

The validator only checks that distinct corners number a power of two and have equal replication. Corners `---, --+, -+-, +--` pass, although their factor contrasts are not balanced or orthogonal. The subsequent independent dot-product coefficient calculations require orthogonality. With responses 1,2,3,4 this accepted design reports R²=7. Validate the actual contrast structure/defining relations, or fit an appropriate general linear model instead of using orthogonal-design formulas.

**15. [P2] SARIMA forecasting accesses negative residual indices for accepted seasonal MA orders — reproduced.**

Locations: [Sarima.cs:48](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/Sarima.cs:48), [Sarima.cs:184](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/Sarima.cs:184).

The length guard considers AR lags but not seasonal MA lags. During fitting, unavailable residual history is correctly treated as zero. Forecasting only checks `t-k < m`, which also admits negative indices. `Sarima.Fit([1,3,2,5,4,7],0,0,0,0,0,1,12,1,false)` passes the initial validation and throws IndexOutOfRangeException during forecasting. Check the lower history bound consistently and reject models whose requested parameters are not estimable from the available data.

**16. [P2] Power calculations ignore the direction of one-sided alternatives — reproduced.**

Location: [Power.cs:62](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/Power.cs:62).

Less and Greater select the same cutoff; the effect is then made absolute. With n=100, p0=0.5, and p1=0.7, both alternatives report power 0.9949103, although the lower-tail test has very low rejection probability under this higher true proportion. The t-test helpers likewise ignore the signed effect entered in the dialog. The two-sided proportion calculation also includes only one rejection tail. Implement direction-specific probabilities and solve sample size against the same power function, rejecting unattainable requests.

**17. [P2] Student-t posterior scale is labeled as posterior standard deviation — reproduced.**

Locations: [BayesianInference.cs:95](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/BayesianInference.cs:95), [BayesianInference.cs:115](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/BayesianInference.cs:115).

Unknown-variance normal inference and Bayesian regression place the t distribution's scale into PosteriorSd. For df>2, standard deviation is scale×sqrt(df/(df−2)); it is not finite at df≤2. For data 1,2,3 the app reports posterior SD≈0.57735 with df=2, while the instantiated Math.NET posterior distribution has infinite SD. Keep scale for interval and tail calculations, but report actual posterior moments separately, including undefined/nonfinite cases. The n=2 mean label also needs care because a df=1 t posterior has no finite mean.

**18. [P2] Valid large counts overflow before conversion to floating point — reproduced.**

Locations: [HypothesisTests.cs:139](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/HypothesisTests.cs:139), [ControlCharts.cs:73](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/Spc/ControlCharts.cs:73), [ControlCharts.cs:119](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/Spc/ControlCharts.cs:119).

TwoProportions adds int counts before casting, so two samples each containing 750 million events out of 1.5 billion trials return NaN instead of the equal-proportions result. PChart/UChart use integer Sum before assignment/casting to double; sample sizes 1.5 billion and 1.5 billion throw OverflowException. Widen each operand or use a widened accumulator before addition. Fisher's exact-test margins use the same int-addition pattern and also need an explicit supported-size policy.

**19. [P2] The variance-test dialog's alternative selection is ignored — static.**

Locations: [MainWindow.xaml.cs:717](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/MainWindow.xaml.cs:717), [VarianceTests.cs:19](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/VarianceTests.cs:19).

TwoColumnWindow exposes and validates Less/Greater/TwoSided for the F-test, but OnTwoVariances passes only Confidence. FTest always computes a two-sided p-value and interval. A user's directional hypothesis never reaches the engine. Either implement and pass the alternative or remove that option from this dialog configuration.

**20. [P2] Boxplots silently omit outlier observations — static.**

Location: [Plots.cs:81](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/Graphs/Plots.cs:81).

Whiskers stop at the last observations inside the 1.5-IQR fences, but values outside those fences are never added to another plottable or passed to Box. A sample such as 1,2,3,4,5,6,7,8,9,100 renders a boxplot with no representation of 100. Plot the excluded observations explicitly so the graph represents the full sample.

**21. [P2] K-means reports convergence even when the iteration limit ends the loop — reproduced.**

Locations: [KMeans.cs:28](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/KMeans.cs:28), [MultivariateFormatters.cs:34](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/MultivariateFormatters.cs:34).

Cluster returns the same result shape whether assignments stabilize or maxIter is exhausted. The formatter always says “converged in … iterations.” A one-iteration run reproduced that claim even though no convergence check had succeeded. At a limit exit, assignments were calculated against the preceding centroids and need not be nearest to the returned updated centroids. Return a termination reason/convergence flag and communicate limit exhaustion.

**Smaller inconsistencies and incomplete branches**

- **[P3] Navigator and dimensions become stale.** [MainWindow.xaml.cs:306](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/MainWindow.xaml.cs:306): row/column changes only mark dirty. RefreshNavigator and UpdateDims are called only when replacing the table, so typing/deleting rows does not refresh counts or inferred types.
- **[P3] Factor-analysis percentages are displayed as fractions.** [MultivariateFormatters.cs:56](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/MultivariateFormatters.cs:56): `% Var` formats Proportion directly, so 50% appears as 0.5000. Multiply by 100 or relabel the row.
- **Moving-average alignment changes implicitly with window parity.** [TimeSeries.cs:103](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Core/Statistics/TimeSeries.cs:103): odd lengths use centered windows containing future observations; even lengths use trailing windows including the current observation. The UI offers no alignment choice and reports Accuracy Measures for both. Centered smoothing is legitimate and explicitly tested, but this parity-dependent semantic switch should be documented or replaced with an explicit mode; these errors should not be presented as out-of-sample forecast validation.
- **Cancellation suppresses publication but does not interrupt a running numerical fit.** [MainWindow.xaml.cs:1342](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/MainWindow.xaml.cs:1342): Task.Run receives a token, but Fit does not. Once started, the worker keeps computing. If active cancellation is intended, it needs cooperative checks inside the numerical work. This is separate from the disposed-source defect.
- **Dead or redundant paths.** [MainWindow.xaml.cs:419](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/MainWindow.xaml.cs:419) defines unused NotYet; [MainWindow.xaml.cs:1762](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/MainWindow.xaml.cs:1762) contains an empty navigator double-click handler. DoubleExp's `n > 1` fallback is unreachable after its minimum-length guard. These are cleanup items rather than major correctness failures.
- **Global recovery claims too much.** [App.xaml.cs:31](C:/Users/dhucu/source/repos/StatStudio/src/StatStudio.Wpf/App.xaml.cs:31) tells users the worksheet is unchanged for every escaped UI exception, although no snapshot/rollback verifies that assertion. Finding 2 demonstrates a state mutation that survives global handling. Recovery messaging should reflect what is actually known.

**Validation gaps exposed by the audit**

The smoke tests exercise Core methods directly, so they do not catch UI-thread property access or the repeated-fit lifecycle. Mixture tests pass full-precision generated arrays directly to the analyzer and bypass worksheet serialization. Fractional-design tests check selected main effects and the existence of aliases but do not assert intercept aliasing, bounded R², or rejection of nonregular corners. Existing normality tests do not exercise large AD statistics, and Bayesian unknown-variance tests check intervals rather than posterior SD.

The most valuable additional checks are therefore complete operation sequences: first fit → second fit → close; create mixture → worksheet strings → analyze; import → display/sort → analyze → save → reload; and result invariants such as finite probabilities in [0,1], consistent row identity, signed hypothesis behavior, and explicit convergence status.


