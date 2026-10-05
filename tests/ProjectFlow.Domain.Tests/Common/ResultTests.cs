using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Tests.Common;

public class ResultTests
{
    private static readonly Error SampleError = Error.Validation("Sample.Code", "Sample description.");

    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_exposes_the_error()
    {
        Result result = SampleError;

        Assert.True(result.IsFailure);
        Assert.Equal(SampleError, result.Error);
    }

    [Fact]
    public void Value_of_a_successful_result_is_available()
    {
        Result<int> result = 42;

        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Value_of_a_failed_result_throws()
    {
        Result<int> result = SampleError;

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Failure_with_no_error_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure(Error.None));
    }
}
