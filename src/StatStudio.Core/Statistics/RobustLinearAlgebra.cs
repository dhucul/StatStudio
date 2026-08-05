using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;

namespace StatStudio.Core.Statistics;

internal static class RobustLinearAlgebra
{
    /// <summary>
    /// Full-rank OLS via thin QR, returning the coefficients and (XᵀX)⁻¹.
    /// </summary>
    /// <remarks>
    /// Deliberately not SVD: Math.NET's dense SVD materialises U at RowCount × RowCount, so a
    /// 10 000-row regression allocates 800 MB for a factor this method never reads. Thin QR is
    /// O(np²) in time and O(np) in space, and is as numerically sound as SVD for a full-rank
    /// least-squares solve. Rank is screened from the R diagonal on the same tolerance the SVD
    /// path used, so callers still see the identical ArgumentException for a singular design.
    /// </remarks>
    public static (Vector<double> Solution, Matrix<double> GramInverse) LeastSquares(
        Matrix<double> design, Vector<double> response, string singularMessage)
    {
        int p = design.ColumnCount;
        if (design.RowCount < p) throw new ArgumentException(singularMessage);

        var qr = design.QR(QRMethod.Thin);
        // Thin QR gives a p x p R; take the leading block so a Full factorisation would work too.
        var r = qr.R.SubMatrix(0, p, 0, p);
        RequireFullColumnRank(design, r, singularMessage);

        var solution = qr.Solve(response);
        var rInverse = r.Inverse();
        // X = QR  =>  XᵀX = RᵀR  =>  (XᵀX)⁻¹ = R⁻¹R⁻ᵀ
        var gramInverse = rInverse * rInverse.Transpose();
        return (solution, gramInverse);
    }

    public static (Vector<double> Solution, Matrix<double> Inverse) SolveSquare(
        Matrix<double> matrix, Vector<double> rightHandSide, string singularMessage)
    {
        var svd = matrix.Svd(computeVectors: true);
        RequireFullColumnRank(matrix, svd.S, singularMessage);
        var solution = svd.Solve(rightHandSide);
        var inverse = svd.Solve(Matrix<double>.Build.DenseIdentity(matrix.RowCount));
        return (solution, inverse);
    }

    private static void RequireFullColumnRank(Matrix<double> matrix, Vector<double> singularValues, string message)
    {
        if (matrix.RowCount < matrix.ColumnCount || singularValues.Count < matrix.ColumnCount)
            throw new ArgumentException(message);
        double largest = singularValues.Count == 0 ? 0 : singularValues[0];
        double tolerance = Math.Max(matrix.RowCount, matrix.ColumnCount) * Math.Max(1.0, largest) * 1e-12;
        if (singularValues.Take(matrix.ColumnCount).Any(s => !double.IsFinite(s) || s <= tolerance))
            throw new ArgumentException(message);
    }

    /// <summary>Rank screen from the triangular factor: a collinear column leaves a ~0 pivot on R's diagonal.</summary>
    private static void RequireFullColumnRank(Matrix<double> matrix, Matrix<double> r, string message)
    {
        int p = matrix.ColumnCount;
        if (r.RowCount < p || r.ColumnCount < p) throw new ArgumentException(message);

        double largest = 0;
        for (int i = 0; i < p; i++) largest = Math.Max(largest, Math.Abs(r[i, i]));
        double tolerance = Math.Max(matrix.RowCount, p) * Math.Max(1.0, largest) * 1e-12;

        for (int i = 0; i < p; i++)
        {
            double pivot = Math.Abs(r[i, i]);
            if (!double.IsFinite(pivot) || pivot <= tolerance) throw new ArgumentException(message);
        }
    }
}
