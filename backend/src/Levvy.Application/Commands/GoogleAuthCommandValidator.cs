using FluentValidation;

namespace Levvy.Application.Commands;

public sealed class GoogleAuthCommandValidator : AbstractValidator<GoogleAuthCommand>
{
    public GoogleAuthCommandValidator()
    {
        RuleFor(x => x.IdToken).NotEmpty().MaximumLength(8192);
        RuleFor(x => x.Institution).MaximumLength(160);
        RuleFor(x => x.Department).MaximumLength(120);
        RuleFor(x => x.PreferredLanguage).MaximumLength(12);
    }
}
