using FluentValidation;
using Mediator;
using ProjectFlow.Application.Behaviors;

namespace ProjectFlow.Application.Tests.Behaviors;

public class ValidationBehaviorTests
{
    private sealed record SampleCommand(string Name) : ICommand<string>;

    private sealed class SampleCommandValidator : AbstractValidator<SampleCommand>
    {
        public SampleCommandValidator() => RuleFor(command => command.Name).NotEmpty();
    }

    private static ValueTask<string> Next(SampleCommand command, CancellationToken cancellationToken) =>
        ValueTask.FromResult("handled");

    [Fact]
    public async Task Calls_next_when_message_is_valid()
    {
        var behavior = new ValidationBehavior<SampleCommand, string>([new SampleCommandValidator()]);

        var result = await behavior.Handle(new SampleCommand("Board"), Next, CancellationToken.None);

        Assert.Equal("handled", result);
    }

    [Fact]
    public async Task Calls_next_when_there_are_no_validators()
    {
        var behavior = new ValidationBehavior<SampleCommand, string>([]);

        var result = await behavior.Handle(new SampleCommand(""), Next, CancellationToken.None);

        Assert.Equal("handled", result);
    }

    [Fact]
    public async Task Throws_validation_exception_when_message_is_invalid()
    {
        var behavior = new ValidationBehavior<SampleCommand, string>([new SampleCommandValidator()]);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => behavior.Handle(new SampleCommand(""), Next, CancellationToken.None).AsTask());

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(SampleCommand.Name));
    }
}
