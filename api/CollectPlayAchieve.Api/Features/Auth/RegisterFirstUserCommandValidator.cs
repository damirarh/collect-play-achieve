using FluentValidation;

namespace CollectPlayAchieve.Api.Features.Auth;

internal sealed class RegisterFirstUserCommandValidator
    : AbstractValidator<RegisterFirstUserCommand>
{
    public RegisterFirstUserCommandValidator()
    {
        RuleFor(command => command.Username).NotNull().WithMessage("Username is required.");
        RuleFor(command => command.Password).NotNull().WithMessage("Password is required.");
    }
}
