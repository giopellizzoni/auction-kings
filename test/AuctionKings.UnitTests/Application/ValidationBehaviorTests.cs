using AuctionKings.Application.Behaviors;
using FluentValidation;
using MediatR;
using AppValidationException = AuctionKings.Application.Exceptions.ValidationException;

namespace AuctionKings.UnitTests.Application;

public sealed class ValidationBehaviorTests
{
    private sealed record TestRequest(string Name) : IRequest<string>;

    private sealed class FailingValidator : AbstractValidator<TestRequest>
    {
        public FailingValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
        }
    }

    private sealed class PassingValidator : AbstractValidator<TestRequest>
    {
        public PassingValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }

    [Fact]
    public async Task ValidationBehavior_WithInvalidRequest_ShouldThrowValidationException()
    {
        var validators = new IValidator<TestRequest>[] { new FailingValidator() };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);

        var request = new TestRequest(string.Empty);

        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");

        var exception = await Assert.ThrowsAsync<AppValidationException>(() =>
            behavior.Handle(request, next, CancellationToken.None)
        );

        Assert.True(exception.Errors.Count > 0);
        Assert.Contains("Name", exception.Errors.Keys);
    }

    [Fact]
    public async Task ValidationBehavior_WithValidRequest_ShouldCallNext()
    {
        var validators = new IValidator<TestRequest>[] { new PassingValidator() };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);

        var request = new TestRequest("Valid Name");

        var nextCalled = false;
        RequestHandlerDelegate<string> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult("response");
        };

        var result = await behavior.Handle(request, next, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.Equal("response", result);
    }

    [Fact]
    public async Task ValidationBehavior_WithNoValidators_ShouldCallNext()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([]);

        var request = new TestRequest(string.Empty);
        var nextCalled = false;
        RequestHandlerDelegate<string> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult("response");
        };

        await behavior.Handle(request, next, CancellationToken.None);

        Assert.True(nextCalled);
    }
}
