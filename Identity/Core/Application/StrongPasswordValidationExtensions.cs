using FluentValidation;

namespace QualifyAI.Identity.Application;

public static class StrongPasswordValidationExtensions
{
    public static IRuleBuilderOptions<T, string> StrongIdentityPassword<T>(
        this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty()
            .MinimumLength(10)
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a number.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a special character.");
}
