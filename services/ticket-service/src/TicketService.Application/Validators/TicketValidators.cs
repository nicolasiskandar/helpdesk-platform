using FluentValidation;
using TicketService.Application.DTOs;

namespace TicketService.Application.Validators;

public class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(5000).WithMessage("Description must not exceed 5000 characters.");
        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("A valid category is required.");
        RuleFor(x => x.PriorityId)
            .GreaterThan(0).WithMessage("A valid priority is required.");
    }
}

public class UpdateTicketRequestValidator : AbstractValidator<UpdateTicketRequest>
{
    public UpdateTicketRequestValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");
        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Description must not exceed 5000 characters.");
    }
}

public class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Comment content is required.")
            .MaximumLength(5000).WithMessage("Comment must not exceed 5000 characters.");
    }
}
