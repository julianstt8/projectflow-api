using FluentValidation;
using Mediator;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Application.Authentication.Register;

public sealed record RegisterUserCommand(string Email, string Password, string FullName) : ICommand<Result<UserResponse>>;

public sealed record UserResponse(Guid Id, string Email, string FullName);

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    public RegisterUserCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(Domain.Users.Email.MaxLength);
        RuleFor(command => command.FullName).NotEmpty().MaximumLength(User.FullNameMaxLength);
        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(PasswordMinLength)
            .MaximumLength(PasswordMaxLength)
            .Matches("[A-Za-z]").WithMessage("The password must contain at least one letter.")
            .Matches("[0-9]").WithMessage("The password must contain at least one digit.");
    }
}

public sealed class RegisterUserCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
    : ICommandHandler<RegisterUserCommand, Result<UserResponse>>
{
    public async ValueTask<Result<UserResponse>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var email = Email.Create(command.Email);
        if (email.IsFailure)
        {
            return email.Error;
        }

        if (await users.ExistsWithEmailAsync(email.Value, cancellationToken))
        {
            return AuthenticationErrors.EmailAlreadyRegistered;
        }

        var user = User.Create(email.Value, passwordHasher.Hash(command.Password), command.FullName, timeProvider.GetUtcNow());
        if (user.IsFailure)
        {
            return user.Error;
        }

        users.Add(user.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserResponse(user.Value.Id, user.Value.Email.Value, user.Value.FullName);
    }
}
