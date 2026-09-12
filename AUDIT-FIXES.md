# Logic audit remediation

The findings in the whole-application audits have been addressed in the current source. The original audit remains a historical record. This note records the chosen behavior and validation.

| Finding | Resolution |
| --- | --- |
| UI controls read by ARIMA/SARIMA workers | Dialog choices are captured before scheduling; constant options are stored values. |
| Disposed cancellation source and disabled-window failure | `BackgroundWork` owns one source, rejects overlapping work, clears it before disposal, and restores controls in `finally`, including setup failures. Closing cancels work and suppresses late publication. |
| Fractional intercept and signed aliases | Register the intercept, canonicalize contrast signs, validate affine closure of regular fractions, and model center-point shifts separately. R² uses residual pure error. |
| Invalid large normality p-values and verdicts | Stop the quadratic tail approximation before its reversal; invalid probabilities produce an inconclusive verdict. |
| Mixture rounding | Generated component values and the Concrete Mixture sample retain full precision in storage. |
| Missing periods | Ordered analyses reject gaps before the final observation; trailing padding is ignored. |
| Sorting differs from analysis order | Header sorting is disabled. The displayed row order, analysis snapshots, and exports remain in worksheet order. |
| Header inference and empty/numeric headers | Missing markers are not header evidence, single textual headers can describe empty worksheets, and the import dialog explicitly selects header presence. |
| Calculator precision/overflow | `EvaluateIntoColumn` evaluates before replacement, preserves round-trip precision, and rejects infinity without overwriting the original column. |
| Declared column types lost through the grid | Store type metadata on `DataTable` columns and restore it to Core snapshots. Explicit Text/DateTime columns are excluded from numeric pickers. |
| Seasonal MA negative indexing | Validate effective history/parameter counts and guard both bounds of residual lookups. |
| Power direction and missing rejection tail | Retain signed effects, calculate the selected rejection region, include both two-sided tails, and search for the minimum whole sample size. Unattainable directional targets raise a clear error. |
| Posterior scale labeled as SD | Calculate Student-t SD separately from its interval scale; represent undefined means and non-finite variances correctly. |
| Calculator evaluation depth | Bound total expression length in addition to recursive parser depth; quoted column references resolve by name. |
| Installer wildcard deletion | Remove `InstallDelete` patterns. Reject nonempty destinations unless registered to this application, replace only payload files, and request graceful application closure. Compiler discovery supports Inno Setup 7 and 6. |
| Formula-guard round trips | External CSVs preserve literal apostrophes. Explicit StatStudio CSV mode decodes a reversible escape for both values and headers. XLSX handles ClosedXML's leading-apostrophe behavior and stores quote-prefix metadata. |
| Integer totals overflow | Widen before summing proportion/SPC counts; validate Fisher margins and bound exact enumeration. |
| Ignored dialog selections | The variance dialog exposes only its supported two-sided choice; I-MR requires exactly one selected column. |
| False K-means convergence | Return and display the actual convergence flag, including iteration exhaustion. |
| Boxplot outliers disappear | Render outliers explicitly as scatter markers. |
| Stale navigator/dimensions | Coalesce data-change events and refresh derived worksheet summaries on the dispatcher. |
| Factor percentages mislabeled | Format proportions as percentages under `% Var`. |
| Expensive operations and missing cancellation | File operations and heavier analyses run off the UI thread. Fitting, model searches, clustering, smoothing, calculator, and file loops have cooperative cancellation checks. A save that has already committed wins a later cancellation click. |
| Unbounded allocations | Shared limits cover forecasts/designs (10,000), worksheet cells (2,000,000), columns (512), and imported file size (64 MB). |
| Moving-average parity changes alignment | Every window length uses trailing smoothing, identified in the output label. |
| Dead paths and overconfident global recovery | Remove unused placeholder/navigation handlers and the unreachable initial-trend fallback. Unexpected-error messages no longer claim an unverified rollback. |

## Validation

- `dotnet test StatStudio.slnx -c Release --no-restore --verbosity minimal`: **49 passing test cases/suites** (41 Core/data, 8 WPF), zero failures.
- Tests cover all catalogued fractional designs, negative aliases, center points, large normality statistics, mixture storage/import, CSV and XLSX literals, calculator precision and failure atomicity, signed power, Bayesian moments, count overflow, convergence, missing periods, cancellation during running work, and unchanged destinations on cancellation.
- WPF tests run on STA dispatchers and exercise actual window/dialog classes, repeated fits, errors, cancellation, closing, declared types, summary refresh, and outlier markers without displaying windows.
- Inno Setup 7 compiled `installer/StatStudio.iss` successfully into a separate validation artifact. This verifies the script against the existing packaging payload; it is not an installation/uninstallation test or a newly published release installer.
- Whitespace/error checks passed. The application source was not committed or deployed.

## Behavior to be aware of

Header sorting is deliberately disabled rather than allowing visual order to silently diverge. Importing data now asks about headers; enable the StatStudio CSV option only for its guarded exports. Moving averages are consistently trailing. The variance test remains two-sided and says so. Missing-period analyses fail with the offending row instead of compressing time.

Cancellation is cooperative. Third-party workbook/matrix calls finish their current call before the surrounding cancellation checks can take effect. Existing files that already lost precision or apostrophes cannot have their original values reconstructed by these fixes.

Implementation references checked: [ClosedXML cell-value semantics](https://docs.closedxml.io/en/latest/api/index.html), [Inno Setup installation order](https://jrsoftware.org/ishelp/topic_installorder.htm), and [Inno Setup directory enumeration](https://jrsoftware.org/ishelp/topic_isxfunc_findfirst.htm).

## Installer upgrade correction — 2.2.2

The initial folder guard used a registry identity with one trailing brace, while released
installers registered an identity with two. That incorrectly rejected the existing installation.
`installer/AppIdentity.iss` now supplies the unchanged deployed identity to both `[Setup]` and
the registration lookup. The lookup checks machine/user registrations in both registry views.

The shared Pascal validation code was exercised against the actual installed 2.2.0 directory,
case-insensitive paths, empty/new directories, and an unrelated nonempty directory. All cases
passed, and hashes confirmed the installed application and unrelated file were unchanged.
The probe always aborts in `InitializeSetup`; it does not perform an installation.

To repeat this check, compile `tools/installer-upgrade-probe.iss`, then run
`powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-installer-upgrade.ps1 -InstalledDir "C:\Program Files\StatStudio"`.
