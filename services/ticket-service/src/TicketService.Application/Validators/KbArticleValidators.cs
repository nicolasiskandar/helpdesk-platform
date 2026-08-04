using FluentValidation;
using TicketService.Application.DTOs;

namespace TicketService.Application.Validators;

public class KbArticleRequestValidator : AbstractValidator<KbArticleRequest>
{
    public KbArticleRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");
        RuleFor(x => x.Excerpt)
            .MaximumLength(500).WithMessage("Excerpt must not exceed 500 characters.");
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Body is required.");
        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(100).WithMessage("Category must not exceed 100 characters.");
        RuleFor(x => x.Status)
            .Must(s => s is "published" or "draft")
            .WithMessage("Status must be either 'published' or 'draft'.");
    }
}
