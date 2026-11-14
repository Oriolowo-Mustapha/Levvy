using FluentValidation;

namespace Levvy.Application.Commands;

public sealed class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Institution).MaximumLength(160);
        RuleFor(x => x.Department).MaximumLength(120);
        RuleFor(x => x.PreferredLanguage).MaximumLength(12);
        RuleFor(x => x.AvatarUrl).MaximumLength(1024);
    }
}
