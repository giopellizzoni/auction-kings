using AuctionKings.Shared.Common;

namespace AuctionKings.UnitTests.Shared;

public class ResultTests
{
    [Fact]
    public void Result_Success_ShouldBeSuccessful()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Result_Failure_ShouldHaveError()
    {
        var error = Error.Validation("INVALID", "Value is invalid.");
        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Result_Success_ShouldHaveValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Result_Failure_ShouldHaveError_Generic()
    {
        var error = Error.NotFound("NOT_FOUND", "Entity not found.");
        var result = Result.Failure<int>(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Result_Failure_AccessingValue_ShouldThrow()
    {
        var error = Error.Conflict("CONFLICT", "Duplicate bid.");
        var result = Result.Failure<string>(error);

        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void Result_ImplicitConversion_ShouldCreateSuccess()
    {
        Result<string> result = "hello";

        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void Result_SuccessWithError_ShouldThrowOnConstruction()
    {
        // Constructing with isSuccess=true but non-None error should throw
        Assert.Throws<InvalidOperationException>(() => Result.Failure<int>(Error.None));
    }

    [Fact]
    public void Error_Record_ShouldBeValueEqual()
    {
        var a = new Error("CODE", "Message");
        var b = new Error("CODE", "Message");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Error_ToString_ShouldContainCodeAndMessage()
    {
        var error = new Error("E001", "Something failed");
        Assert.Contains("E001", error.ToString());
        Assert.Contains("Something failed", error.ToString());
    }
}
