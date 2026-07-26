using MathNet.Numerics.LinearAlgebra;

namespace StatStudio.Core.Statistics;

internal static class RobustLinearAlgebra
{
    public static (Vector<double> Solution, Matrix<double> GramInverse) LeastSquares(
        Matrix<double> design, Vector<double> response, string singularMessage)
    {
        var svd = design.Svd(computeVectors: true);
        RequireFullColumnRank(design, svd.S, singularMessage);
        var solution = svd.Solve(response);

        int p = design.ColumnCount;
        var v = svd.VT.Transpose();
        var inverseSquares = Matrix<double>.Build.DenseDiagonal(p, p,
            i => 1.0 / (svd.S[i] * svd.S[i]));
        var gramInverse = v * inverseSquares * v.Transpose();
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
}
