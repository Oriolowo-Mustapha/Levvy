using FluentValidation;

namespace Levvy.Application.Commands;

/// <summary>
/// Sample validator: sets the pattern every command copies on the 14th.
/// Boundary input is validated here, never trusted from the wire.
/// </summary>
public class AskQuestionCommandValidator : AbstractValidator<AskQuestionCommand>
{
    public AskQuestionCommandValidator()
    {
        RuleFor(x => x.SessionCode).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(500);
    }
}
