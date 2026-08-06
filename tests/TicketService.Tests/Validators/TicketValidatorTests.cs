using FluentAssertions;
using FluentValidation.TestHelper;
using TicketService.Application.DTOs;
using TicketService.Application.Validators;
using Xunit;

namespace TicketService.Tests.Validators;

public class TicketValidatorTests
{
    // ---------- CreateTicketRequestValidator ----------

    [Fact]
    public void CreateTicket_ValidInput_ReturnsValid()
    {
        var validator = new CreateTicketRequestValidator();
        var request = new CreateTicketRequest("Printer not working", "Cannot print from the 3rd floor", 1, 2);

        var result = validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateTicket_MissingTitle_ReturnsInvalid(string title)
    {
        var validator = new CreateTicketRequestValidator();
        var request = new CreateTicketRequest(title, "Description", 1, 1);

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void CreateTicket_OverlongTitle_ReturnsInvalid()
    {
        var validator = new CreateTicketRequestValidator();
        var request = new CreateTicketRequest(new string('a', 201), "Description", 1, 1);

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateTicket_MissingDescription_ReturnsInvalid(string description)
    {
        var validator = new CreateTicketRequestValidator();
        var request = new CreateTicketRequest("Title", description, 1, 1);

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void CreateTicket_OverlongDescription_ReturnsInvalid()
    {
        var validator = new CreateTicketRequestValidator();
        var request = new CreateTicketRequest("Title", new string('a', 5001), 1, 1);

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateTicket_InvalidCategoryId_ReturnsInvalid(int categoryId)
    {
        var validator = new CreateTicketRequestValidator();
        var request = new CreateTicketRequest("Title", "Description", categoryId, 1);

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.CategoryId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateTicket_InvalidPriorityId_ReturnsInvalid(int priorityId)
    {
        var validator = new CreateTicketRequestValidator();
        var request = new CreateTicketRequest("Title", "Description", 1, priorityId);

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PriorityId);
    }

    // ---------- UpdateTicketRequestValidator ----------

    [Fact]
    public void UpdateTicket_ValidInput_ReturnsValid()
    {
        var validator = new UpdateTicketRequestValidator();
        var request = new UpdateTicketRequest("New title", "New description", 2, 3);

        var result = validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateTicket_OverlongTitle_ReturnsInvalid()
    {
        var validator = new UpdateTicketRequestValidator();
        var request = new UpdateTicketRequest(new string('a', 201), null, null, null);

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void UpdateTicket_OverlongDescription_ReturnsInvalid()
    {
        var validator = new UpdateTicketRequestValidator();
        var request = new UpdateTicketRequest(null, new string('a', 5001), null, null);

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    // ---------- AddCommentRequestValidator ----------

    [Fact]
    public void AddComment_ValidInput_ReturnsValid()
    {
        var validator = new AddCommentRequestValidator();
        var request = new AddCommentRequest("Thanks, that helped", false);

        var result = validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddComment_MissingContent_ReturnsInvalid(string content)
    {
        var validator = new AddCommentRequestValidator();
        var request = new AddCommentRequest(content, false);

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void AddComment_OverlongContent_ReturnsInvalid()
    {
        var validator = new AddCommentRequestValidator();
        var request = new AddCommentRequest(new string('a', 5001), false);

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Content);
    }

    // ---------- KbArticleRequestValidator ----------

    [Fact]
    public void KbArticle_ValidInput_ReturnsValid()
    {
        var validator = new KbArticleRequestValidator();
        var request = new KbArticleRequest("How to reset a password", "Steps for admins", "Click here...", "Access", "published");

        var result = validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("archived")]
    [InlineData("")]
    [InlineData("live")]
    public void KbArticle_InvalidStatus_ReturnsInvalid(string status)
    {
        var validator = new KbArticleRequestValidator();
        var request = new KbArticleRequest("Title", "Excerpt", "Body", "Access", status);

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void KbArticle_MissingTitle_ReturnsInvalid()
    {
        var validator = new KbArticleRequestValidator();
        var request = new KbArticleRequest("", "Excerpt", "Body", "Access", "published");

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void KbArticle_MissingBody_ReturnsInvalid()
    {
        var validator = new KbArticleRequestValidator();
        var request = new KbArticleRequest("Title", "Excerpt", "", "Access", "published");

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Body);
    }

    [Fact]
    public void KbArticle_MissingCategory_ReturnsInvalid()
    {
        var validator = new KbArticleRequestValidator();
        var request = new KbArticleRequest("Title", "Excerpt", "Body", "", "published");

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Category);
    }
}
