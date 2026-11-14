using FluentValidation;

namespace Levvy.Application.Commands;

public sealed class JoinSessionCommandValidator : AbstractValidator<JoinSessionCommand>
{
    public JoinSessionCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.PreferredLanguage).NotEmpty().MaximumLength(12);
    }
}
