using Xunit;

namespace StatStudio.Smoke;

[Collection("Smoke suites")]
public sealed class SmokeTests
{
    [Theory]
    [InlineData("Data")]
    [InlineData("Statistics")]
    [InlineData("Hypothesis")]
    [InlineData("Regression")]
    [InlineData("SPC")]
    [InlineData("Nonparametric")]
    [InlineData("Advanced")]
    [InlineData("Time series")]
    [InlineData("Multivariate")]
    [InlineData("DOE")]
    [InlineData("Factor and reliability")]
    [InlineData("Bayesian and mixed")]
    [InlineData("Edge contracts")]
    public void SuitePasses(string suite)
    {
        Check.Reset();
        switch (suite)
        {
            case "Data": DataTests.Run(); break;
            case "Statistics": StatTests.Run(); break;
            case "Hypothesis": HypoTests.Run(); break;
            case "Regression": RegrTests.Run(); break;
            case "SPC": SpcTests.Run(); break;
            case "Nonparametric": NonparTests.Run(); break;
            case "Advanced": AdvancedTests.Run(); break;
            case "Time series": TimeSeriesTests.Run(); break;
            case "Multivariate": MultiTests.Run(); break;
            case "DOE": DoeTests.Run(); break;
            case "Factor and reliability": FaRelTests.Run(); break;
            case "Bayesian and mixed": BayesMixedTests.Run(); break;
            case "Edge contracts": EdgeTests.Run(); break;
            default: throw new ArgumentOutOfRangeException(nameof(suite));
        }

        Assert.Equal(0, Check.Failed);
    }
}

[CollectionDefinition("Smoke suites", DisableParallelization = true)]
public sealed class SmokeSuiteCollection { }
