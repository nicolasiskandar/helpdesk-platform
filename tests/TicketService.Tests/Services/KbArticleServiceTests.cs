using FluentAssertions;
using Moq;
using TicketService.Application.DTOs;
using TicketService.Domain.Entities;
using TicketService.Domain.Interfaces;
using TicketService.Infrastructure.Services;
using Xunit;

namespace TicketService.Tests.Services;

public class KbArticleServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IKbArticleRepository> _kbRepoMock = new();
    private readonly KbArticleService _sut;

    public KbArticleServiceTests()
    {
        _unitOfWorkMock.Setup(u => u.KbArticles).Returns(_kbRepoMock.Object);
        _sut = new KbArticleService(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task GetArticlesAsync_ReturnsPaginatedResults()
    {
        // Arrange
        var articles = new List<KbArticle>
        {
            new() { Id = Guid.NewGuid(), Title = "VPN guide", Excerpt = "Connect", Body = "Steps", Category = "Network", Status = "published" },
            new() { Id = Guid.NewGuid(), Title = "Password reset", Excerpt = "Reset", Body = "Steps", Category = "Access", Status = "published" }
        };
        _kbRepoMock.Setup(r => r.GetAsync(null, null, 1, 20, false)).ReturnsAsync(articles);
        _kbRepoMock.Setup(r => r.GetCountAsync(null, null, false)).ReturnsAsync(2);

        // Act
        var result = await _sut.GetArticlesAsync(null, null, 1, 20, false);

        // Assert
        result.Articles.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Articles[0].Title.Should().Be("VPN guide");
    }

    [Fact]
    public async Task GetArticleAsync_IncrementsViews()
    {
        // Arrange
        var id = Guid.NewGuid();
        var article = new KbArticle
        {
            Id = id,
            Title = "VPN guide",
            Body = "Steps",
            Category = "Network",
            Status = "published",
            Views = 5
        };
        _kbRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(article);

        // Act
        var result = await _sut.GetArticleAsync(id, false);

        // Assert
        result.Views.Should().Be(6);
        _kbRepoMock.Verify(r => r.UpdateAsync(It.Is<KbArticle>(a => a.Views == 6)), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetArticleAsync_DraftHiddenFromNonAdmin_ThrowsKeyNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var article = new KbArticle { Id = id, Title = "Draft", Body = "x", Category = "Other", Status = "draft" };
        _kbRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(article);

        // Act
        var act = () => _sut.GetArticleAsync(id, false);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
        _kbRepoMock.Verify(r => r.UpdateAsync(It.IsAny<KbArticle>()), Times.Never);
    }

    [Fact]
    public async Task GetArticleAsync_NotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _kbRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((KbArticle?)null);

        // Act
        var act = () => _sut.GetArticleAsync(id, false);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task GetArticleAsync_DraftVisibleToAdmin_IncrementsViews()
    {
        // Arrange
        var id = Guid.NewGuid();
        var article = new KbArticle { Id = id, Title = "Draft", Body = "x", Category = "Other", Status = "draft", Views = 2 };
        _kbRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(article);

        // Act
        var result = await _sut.GetArticleAsync(id, true);

        // Assert
        result.Views.Should().Be(3);
        _kbRepoMock.Verify(r => r.UpdateAsync(It.Is<KbArticle>(a => a.Views == 3)), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetArticlesAsync_WithSearchAndCategory_PassesFiltersToRepository()
    {
        // Arrange
        var articles = new List<KbArticle>
        {
            new() { Id = Guid.NewGuid(), Title = "VPN guide", Body = "Steps", Category = "Network", Status = "published" }
        };
        _kbRepoMock.Setup(r => r.GetAsync("vpn", "Network", 2, 5, false)).ReturnsAsync(articles);
        _kbRepoMock.Setup(r => r.GetCountAsync("vpn", "Network", false)).ReturnsAsync(1);

        // Act
        var result = await _sut.GetArticlesAsync("vpn", "Network", 2, 5, false);

        // Assert
        result.Articles.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(5);
        _kbRepoMock.Verify(r => r.GetAsync("vpn", "Network", 2, 5, false), Times.Once);
        _kbRepoMock.Verify(r => r.GetCountAsync("vpn", "Network", false), Times.Once);
    }

    [Fact]
    public async Task GetArticlesAsync_IncludeDraft_PassesDraftFlagToRepository()
    {
        // Arrange
        var articles = new List<KbArticle>();
        _kbRepoMock.Setup(r => r.GetAsync(null, null, 1, 10, true)).ReturnsAsync(articles);
        _kbRepoMock.Setup(r => r.GetCountAsync(null, null, true)).ReturnsAsync(0);

        // Act
        var result = await _sut.GetArticlesAsync(null, null, 1, 10, true);

        // Assert
        result.Articles.Should().BeEmpty();
        _kbRepoMock.Verify(r => r.GetAsync(null, null, 1, 10, true), Times.Once);
    }

    [Fact]
    public async Task CreateArticleAsync_SetsAuthorAndSaves()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var request = new KbArticleRequest("Title", "Excerpt", "Body", "Software", "published");

        // Act
        var result = await _sut.CreateArticleAsync(request, authorId);

        // Assert
        result.Title.Should().Be("Title");
        result.AuthorUserId.Should().Be(authorId);
        result.Status.Should().Be("published");
        _kbRepoMock.Verify(r => r.AddAsync(It.Is<KbArticle>(a => a.AuthorUserId == authorId)), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateArticleAsync_UpdatesFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        var article = new KbArticle { Id = id, Title = "Old", Body = "Old", Category = "Other", Status = "draft" };
        _kbRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(article);
        var request = new KbArticleRequest("New", "Excerpt", "New body", "Access", "published");

        // Act
        var result = await _sut.UpdateArticleAsync(id, request);

        // Assert
        result.Title.Should().Be("New");
        result.Category.Should().Be("Access");
        result.Status.Should().Be("published");
        _kbRepoMock.Verify(r => r.UpdateAsync(It.Is<KbArticle>(a => a.Title == "New")), Times.Once);
    }

    [Fact]
    public async Task UpdateArticleAsync_NotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _kbRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((KbArticle?)null);
        var request = new KbArticleRequest("New", "Excerpt", "Body", "Other", "published");

        // Act
        var act = () => _sut.UpdateArticleAsync(id, request);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteArticleAsync_DeletesArticle()
    {
        // Arrange
        var id = Guid.NewGuid();
        var article = new KbArticle { Id = id, Title = "Title", Body = "Body", Category = "Other", Status = "published" };
        _kbRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(article);

        // Act
        await _sut.DeleteArticleAsync(id);

        // Assert
        _kbRepoMock.Verify(r => r.DeleteAsync(article), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteArticleAsync_NotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _kbRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((KbArticle?)null);

        // Act
        var act = () => _sut.DeleteArticleAsync(id);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found*");
        _kbRepoMock.Verify(r => r.DeleteAsync(It.IsAny<KbArticle>()), Times.Never);
    }
}
