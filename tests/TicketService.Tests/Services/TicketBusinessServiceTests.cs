using FluentAssertions;
using Moq;
using TicketService.Application.DTOs;
using TicketService.Domain.Entities;
using TicketService.Domain.Interfaces;
using TicketService.Infrastructure.Services;
using Xunit;

namespace TicketService.Tests.Services;

public class TicketBusinessServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IReferenceNumberGenerator> _referenceNumberGeneratorMock = new();
    private readonly TicketBusinessService _sut;

    private readonly Mock<ITicketRepository> _ticketRepoMock = new();
    private readonly Mock<ICategoryRepository> _categoryRepoMock = new();
    private readonly Mock<IPriorityRepository> _priorityRepoMock = new();
    private readonly Mock<IStatusRepository> _statusRepoMock = new();
    private readonly Mock<ITicketAssignmentRepository> _assignmentRepoMock = new();
    private readonly Mock<ITicketCommentRepository> _commentRepoMock = new();
    private readonly Mock<ITicketAttachmentRepository> _attachmentRepoMock = new();
    private readonly Mock<ITicketAuditLogRepository> _auditLogRepoMock = new();
    private readonly Mock<IOutboxRepository> _outboxRepoMock = new();

    public TicketBusinessServiceTests()
    {
        _unitOfWorkMock.Setup(u => u.Tickets).Returns(_ticketRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Categories).Returns(_categoryRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Priorities).Returns(_priorityRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Statuses).Returns(_statusRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.TicketAssignments).Returns(_assignmentRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.TicketComments).Returns(_commentRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.TicketAttachments).Returns(_attachmentRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.TicketAuditLogs).Returns(_auditLogRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Outbox).Returns(_outboxRepoMock.Object);

        _sut = new TicketBusinessService(_unitOfWorkMock.Object, _referenceNumberGeneratorMock.Object);
    }

    [Fact]
    public async Task CreateTicketAsync_Success_CreatesTicketWithReferenceNumberAndAuditLog()
    {
        // Arrange
        var request = new CreateTicketRequest("Test Title", "Test Description", 1, 1);
        var userId = Guid.NewGuid();

        _categoryRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Category { Id = 1, Name = "Hardware" });
        _priorityRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Priority { Id = 1, Name = "Low", Level = 1 });
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });
        _referenceNumberGeneratorMock.Setup(g => g.GenerateAsync()).ReturnsAsync("TKT-000001");

        // Act
        var result = await _sut.CreateTicketAsync(request, userId);

        // Assert
        result.ReferenceNumber.Should().Be("TKT-000001");
        result.Title.Should().Be("Test Title");
        result.CategoryName.Should().Be("Hardware");
        result.PriorityName.Should().Be("Low");
        result.StatusName.Should().Be("Open");
        result.CreatedByUserId.Should().Be(userId);

        _ticketRepoMock.Verify(r => r.AddAsync(It.Is<Ticket>(t =>
            t.ReferenceNumber == "TKT-000001" &&
            t.Title == "Test Title" &&
            t.CreatedByUserId == userId
        )), Times.Once);

        _auditLogRepoMock.Verify(r => r.AddAsync(It.Is<TicketAuditLogEntry>(e =>
            e.FieldChanged == "Created" &&
            e.ChangedByUserId == userId
        )), Times.Once);

        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.created"
        )), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTicketAsync_InvalidCategory_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new CreateTicketRequest("Test Title", "Test Description", 999, 1);
        var userId = Guid.NewGuid();

        _categoryRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Category?)null);

        // Act
        var act = () => _sut.CreateTicketAsync(request, userId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid category*");
    }

    [Fact]
    public async Task GetTicketByIdAsync_Found_ReturnsTicketResponse()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test Ticket",
            Description = "Description",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);

        // Act
        var result = await _sut.GetTicketByIdAsync(ticketId);

        // Assert
        result.Id.Should().Be(ticketId);
        result.ReferenceNumber.Should().Be("TKT-000001");
        result.Title.Should().Be("Test Ticket");
    }

    [Fact]
    public async Task GetTicketByIdAsync_NotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync((Ticket?)null);

        // Act
        var act = () => _sut.GetTicketByIdAsync(ticketId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Ticket not found*");
    }

    [Fact]
    public async Task AssignAgentAsync_Success_CreatesAssignmentAndAuditLog()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var agentUserId = Guid.NewGuid();
        var assignedByUserId = Guid.NewGuid();

        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetActiveAssignmentAsync(ticketId, agentUserId))
            .ReturnsAsync((TicketAssignment?)null);

        // Act
        var result = await _sut.AssignAgentAsync(ticketId, new AssignAgentRequest(agentUserId), assignedByUserId);

        // Assert
        result.AgentUserId.Should().Be(agentUserId);
        result.AssignedByUserId.Should().Be(assignedByUserId);
        result.UnassignedAt.Should().BeNull();

        _assignmentRepoMock.Verify(r => r.AddAsync(It.Is<TicketAssignment>(a =>
            a.TicketId == ticketId &&
            a.AgentUserId == agentUserId &&
            a.AssignedByUserId == assignedByUserId
        )), Times.Once);

        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.assigned"
        )), Times.Once);
    }

    [Fact]
    public async Task AssignAgentAsync_TicketNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync((Ticket?)null);

        // Act
        var act = () => _sut.AssignAgentAsync(ticketId, new AssignAgentRequest(Guid.NewGuid()), Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Ticket not found*");
    }

    [Fact]
    public async Task AssignAgentAsync_DuplicateActiveAssignment_ThrowsInvalidOperationException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var agentUserId = Guid.NewGuid();

        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetActiveAssignmentAsync(ticketId, agentUserId))
            .ReturnsAsync(new TicketAssignment { Id = Guid.NewGuid(), TicketId = ticketId, AgentUserId = agentUserId });

        // Act
        var act = () => _sut.AssignAgentAsync(ticketId, new AssignAgentRequest(agentUserId), Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already assigned*");
    }

    [Fact]
    public async Task ChangeStatusAsync_Success_UpdatesStatusAndPublishesEvent()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        var newStatus = new Status { Id = 2, Name = "In Progress" };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(newStatus);

        // Act
        var result = await _sut.ChangeStatusAsync(ticketId, new ChangeStatusRequest(2, null), userId);

        // Assert
        result.StatusName.Should().Be("In Progress");

        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.status_changed"
        )), Times.Once);
    }

    [Fact]
    public async Task AddCommentAsync_Success_CreatesComment()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var authorUserId = Guid.NewGuid();

        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);

        // Act
        var result = await _sut.AddCommentAsync(ticketId, new AddCommentRequest("Test comment", false), authorUserId);

        // Assert
        result.Content.Should().Be("Test comment");
        result.IsInternal.Should().BeFalse();
        result.AuthorUserId.Should().Be(authorUserId);

        _commentRepoMock.Verify(r => r.AddAsync(It.Is<TicketComment>(c =>
            c.Content == "Test comment" &&
            c.AuthorUserId == authorUserId
        )), Times.Once);
    }

    [Fact]
    public async Task GetCategoriesAsync_ReturnsCategories()
    {
        // Arrange
        var categories = new List<Category>
        {
            new() { Id = 1, Name = "Hardware" },
            new() { Id = 2, Name = "Software" }
        };

        _categoryRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(categories);

        // Act
        var result = await _sut.GetCategoriesAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Hardware");
        result[1].Name.Should().Be("Software");
    }

    [Fact]
    public async Task CreateTicketAsync_InvalidPriority_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new CreateTicketRequest("Test Title", "Test Description", 1, 999);
        var userId = Guid.NewGuid();

        _categoryRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Category { Id = 1, Name = "Hardware" });
        _priorityRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Priority?)null);

        // Act
        var act = () => _sut.CreateTicketAsync(request, userId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid priority*");
    }

    [Fact]
    public async Task CreateTicketAsync_OpenStatusNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new CreateTicketRequest("Test Title", "Test Description", 1, 1);
        var userId = Guid.NewGuid();

        _categoryRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Category { Id = 1, Name = "Hardware" });
        _priorityRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Priority { Id = 1, Name = "Low", Level = 1 });
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync((Status?)null);

        // Act
        var act = () => _sut.CreateTicketAsync(request, userId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Open status not found*");
    }

    [Fact]
    public async Task GetTicketByReferenceNumberAsync_Found_ReturnsTicketResponse()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000042",
            Title = "Test Ticket",
            Description = "Description",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByReferenceNumberAsync("TKT-000042")).ReturnsAsync(ticket);

        // Act
        var result = await _sut.GetTicketByReferenceNumberAsync("TKT-000042");

        // Assert
        result.Id.Should().Be(ticketId);
        result.ReferenceNumber.Should().Be("TKT-000042");
    }

    [Fact]
    public async Task GetTicketByReferenceNumberAsync_NotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _ticketRepoMock.Setup(r => r.GetByReferenceNumberAsync("TKT-999999")).ReturnsAsync((Ticket?)null);

        // Act
        var act = () => _sut.GetTicketByReferenceNumberAsync("TKT-999999");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Ticket not found*");
    }

    [Fact]
    public async Task UpdateTicketAsync_NotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync((Ticket?)null);

        var request = new UpdateTicketRequest("New Title", null, null, null);

        // Act
        var act = () => _sut.UpdateTicketAsync(ticketId, request, Guid.NewGuid(), "Admin");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Ticket not found*");
    }

    [Fact]
    public async Task UpdateTicketAsync_PartialUpdate_OnlyChangesProvidedFields()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Original Title",
            Description = "Original Description",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });

        // Update only Title, leave Description and others unchanged
        var request = new UpdateTicketRequest("Updated Title", null, null, null);

        // Act
        var result = await _sut.UpdateTicketAsync(ticketId, request, userId, "Admin");

        // Assert
        result.Title.Should().Be("Updated Title");
        result.Description.Should().Be("Original Description");

        _auditLogRepoMock.Verify(r => r.AddAsync(It.Is<TicketAuditLogEntry>(e =>
            e.FieldChanged == "Title" &&
            e.OldValue == "Original Title" &&
            e.NewValue == "Updated Title"
        )), Times.Once);
    }

    [Fact]
    public async Task UpdateTicketAsync_InvalidCategory_ThrowsInvalidOperationException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Title",
            Description = "Description",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });
        _categoryRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Category?)null);

        var request = new UpdateTicketRequest(null, null, 999, null);

        // Act
        var act = () => _sut.UpdateTicketAsync(ticketId, request, Guid.NewGuid(), "Admin");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid category*");
    }

    [Fact]
    public async Task UpdateTicketAsync_InvalidPriority_ThrowsInvalidOperationException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Title",
            Description = "Description",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });
        _priorityRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Priority?)null);

        var request = new UpdateTicketRequest(null, null, null, 999);

        // Act
        var act = () => _sut.UpdateTicketAsync(ticketId, request, Guid.NewGuid(), "Admin");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid priority*");
    }

    [Fact]
    public async Task UpdateTicketAsync_NoChanges_ReturnsWithoutSaving()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Title",
            Description = "Description",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });

        // Pass same values — no changes
        var request = new UpdateTicketRequest("Title", "Description", 1, 1);

        // Act
        var result = await _sut.UpdateTicketAsync(ticketId, request, Guid.NewGuid(), "Admin");

        // Assert
        result.Title.Should().Be("Title");
        _ticketRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Ticket>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnassignAgentAsync_Success_UpdatesUnassignedAt()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var agentUserId = Guid.NewGuid();
        var changedByUserId = Guid.NewGuid();

        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        var assignment = new TicketAssignment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AgentUserId = agentUserId,
            AssignedByUserId = Guid.NewGuid(),
            AssignedAt = DateTime.UtcNow
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetActiveAssignmentAsync(ticketId, agentUserId)).ReturnsAsync(assignment);

        // Act
        await _sut.UnassignAgentAsync(ticketId, new UnassignAgentRequest(agentUserId), changedByUserId);

        // Assert
        assignment.UnassignedAt.Should().NotBeNull();
        assignment.UnassignedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _assignmentRepoMock.Verify(r => r.UpdateAsync(assignment), Times.Once);
    }

    [Fact]
    public async Task UnassignAgentAsync_TicketNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync((Ticket?)null);

        // Act
        var act = () => _sut.UnassignAgentAsync(ticketId, new UnassignAgentRequest(Guid.NewGuid()), Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Ticket not found*");
    }

    [Fact]
    public async Task UnassignAgentAsync_AssignmentNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var agentUserId = Guid.NewGuid();

        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetActiveAssignmentAsync(ticketId, agentUserId)).ReturnsAsync((TicketAssignment?)null);

        // Act
        var act = () => _sut.UnassignAgentAsync(ticketId, new UnassignAgentRequest(agentUserId), Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Assignment not found*");
    }

    [Fact]
    public async Task AddAttachmentAsync_Success_CreatesAttachment()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);

        // Act
        var result = await _sut.AddAttachmentAsync(ticketId, "screenshot.png", "/uploads/screenshot.png", userId);

        // Assert
        result.FileName.Should().Be("screenshot.png");
        result.FileUrl.Should().Be("/uploads/screenshot.png");
        result.UploadedByUserId.Should().Be(userId);

        _attachmentRepoMock.Verify(r => r.AddAsync(It.Is<TicketAttachment>(a =>
            a.FileName == "screenshot.png" &&
            a.FileUrl == "/uploads/screenshot.png" &&
            a.UploadedByUserId == userId
        )), Times.Once);
    }

    [Fact]
    public async Task AddAttachmentAsync_TicketNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync((Ticket?)null);

        // Act
        var act = () => _sut.AddAttachmentAsync(ticketId, "file.txt", "/uploads/file.txt", Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Ticket not found*");
    }

    [Fact]
    public async Task GetAttachmentsAsync_ReturnsAttachments()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var attachments = new List<TicketAttachment>
        {
            new() { Id = Guid.NewGuid(), FileName = "file1.pdf", FileUrl = "/uploads/file1.pdf", UploadedByUserId = Guid.NewGuid(), UploadedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), FileName = "file2.png", FileUrl = "/uploads/file2.png", UploadedByUserId = Guid.NewGuid(), UploadedAt = DateTime.UtcNow }
        };

        _attachmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(attachments);

        // Act
        var result = await _sut.GetAttachmentsAsync(ticketId);

        // Assert
        result.Should().HaveCount(2);
        result[0].FileName.Should().Be("file1.pdf");
        result[1].FileName.Should().Be("file2.png");
    }

    [Fact]
    public async Task GetTicketsAsync_ReturnsPaginatedResults()
    {
        // Arrange
        var tickets = new List<Ticket>
        {
            new()
            {
                Id = Guid.NewGuid(), ReferenceNumber = "TKT-000001", Title = "Ticket 1", Description = "Desc",
                CategoryId = 1, PriorityId = 1, StatusId = 1,
                CreatedByUserId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                Category = new Category { Id = 1, Name = "Hardware" },
                Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
                Status = new Status { Id = 1, Name = "Open" }
            }
        };

        _ticketRepoMock.Setup(r => r.GetAllAsync(1, 10, null, null)).ReturnsAsync(tickets);
        _ticketRepoMock.Setup(r => r.GetCountAsync(null, null)).ReturnsAsync(1);

        // Act
        var result = await _sut.GetTicketsAsync(1, 10);

        // Assert
        result.Tickets.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetMyTicketsAsync_FiltersByUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tickets = new List<Ticket>
        {
            new()
            {
                Id = Guid.NewGuid(), ReferenceNumber = "TKT-000001", Title = "My Ticket", Description = "Desc",
                CategoryId = 1, PriorityId = 1, StatusId = 1,
                CreatedByUserId = userId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                Category = new Category { Id = 1, Name = "Hardware" },
                Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
                Status = new Status { Id = 1, Name = "Open" }
            }
        };

        _ticketRepoMock.Setup(r => r.GetByCreatedByUserIdAsync(userId, 1, 10, null, null)).ReturnsAsync(tickets);
        _ticketRepoMock.Setup(r => r.GetCountByCreatedByUserIdAsync(userId, null, null)).ReturnsAsync(1);

        // Act
        var result = await _sut.GetMyTicketsAsync(userId, 1, 10);

        // Assert
        result.Tickets.Should().HaveCount(1);
        result.Tickets[0].CreatedByUserId.Should().Be(userId);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task ChangeStatusAsync_TicketNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync((Ticket?)null);

        // Act
        var act = () => _sut.ChangeStatusAsync(ticketId, new ChangeStatusRequest(2, null), Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Ticket not found*");
    }

    [Fact]
    public async Task ChangeStatusAsync_InvalidStatus_ThrowsInvalidOperationException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Status?)null);

        // Act
        var act = () => _sut.ChangeStatusAsync(ticketId, new ChangeStatusRequest(999, null), Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status*");
    }

    [Fact]
    public async Task AddCommentAsync_TicketNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync((Ticket?)null);

        // Act
        var act = () => _sut.AddCommentAsync(ticketId, new AddCommentRequest("Comment", false), Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Ticket not found*");
    }

    [Fact]
    public async Task GetPrioritiesAsync_ReturnsPriorities()
    {
        // Arrange
        var priorities = new List<Priority>
        {
            new() { Id = 1, Name = "Low", Level = 1 },
            new() { Id = 2, Name = "Medium", Level = 2 },
            new() { Id = 3, Name = "High", Level = 3 }
        };

        _priorityRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(priorities);

        // Act
        var result = await _sut.GetPrioritiesAsync();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Low");
        result[1].Name.Should().Be("Medium");
        result[2].Name.Should().Be("High");
    }

    [Fact]
    public async Task GetStatusesAsync_ReturnsStatuses()
    {
        // Arrange
        var statuses = new List<Status>
        {
            new() { Id = 1, Name = "Open" },
            new() { Id = 2, Name = "In Progress" },
            new() { Id = 3, Name = "Closed" }
        };

        _statusRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(statuses);

        // Act
        var result = await _sut.GetStatusesAsync();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Open");
        result[1].Name.Should().Be("In Progress");
        result[2].Name.Should().Be("Closed");
    }

    [Fact]
    public async Task GetAssignmentsAsync_ReturnsAssignments()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var assignments = new List<TicketAssignment>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, AgentUserId = Guid.NewGuid(), AssignedByUserId = Guid.NewGuid(), AssignedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), TicketId = ticketId, AgentUserId = Guid.NewGuid(), AssignedByUserId = Guid.NewGuid(), AssignedAt = DateTime.UtcNow, UnassignedAt = DateTime.UtcNow }
        };

        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(assignments);

        // Act
        var result = await _sut.GetAssignmentsAsync(ticketId);

        // Assert
        result.Should().HaveCount(2);
        result[0].UnassignedAt.Should().BeNull();
        result[1].UnassignedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCommentsAsync_ReturnsComments()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var comments = new List<TicketComment>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, AuthorUserId = Guid.NewGuid(), Content = "Public comment", IsInternal = false, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), TicketId = ticketId, AuthorUserId = Guid.NewGuid(), Content = "Internal note", IsInternal = true, CreatedAt = DateTime.UtcNow }
        };

        _commentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId, true)).ReturnsAsync(comments);

        // Act
        var result = await _sut.GetCommentsAsync(ticketId, includeInternal: true);

        // Assert
        result.Should().HaveCount(2);
        result[0].Content.Should().Be("Public comment");
        result[0].IsInternal.Should().BeFalse();
        result[1].Content.Should().Be("Internal note");
        result[1].IsInternal.Should().BeTrue();
    }

    [Fact]
    public async Task GetCommentsAsync_ExcludesInternal_OnlyReturnsPublicComments()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var publicComments = new List<TicketComment>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, AuthorUserId = Guid.NewGuid(), Content = "Public comment", IsInternal = false, CreatedAt = DateTime.UtcNow }
        };

        _commentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId, false)).ReturnsAsync(publicComments);

        // Act
        var result = await _sut.GetCommentsAsync(ticketId, includeInternal: false);

        // Assert
        result.Should().HaveCount(1);
        result[0].IsInternal.Should().BeFalse();
    }

    [Fact]
    public async Task GetAuditLogAsync_ReturnsPaginatedEntries()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var entries = new List<TicketAuditLogEntry>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, ChangedByUserId = Guid.NewGuid(), ChangedByType = "User", FieldChanged = "Status", OldValue = "Open", NewValue = "In Progress", ChangedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), TicketId = ticketId, ChangedByUserId = Guid.NewGuid(), ChangedByType = "User", FieldChanged = "Title", OldValue = "Old", NewValue = "New", ChangedAt = DateTime.UtcNow }
        };

        _auditLogRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId, 1, 10)).ReturnsAsync(entries);
        _auditLogRepoMock.Setup(r => r.GetCountByTicketIdAsync(ticketId)).ReturnsAsync(2);

        // Act
        var result = await _sut.GetAuditLogAsync(ticketId, 1, 10);

        // Assert
        result.Entries.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Entries[0].FieldChanged.Should().Be("Status");
        result.Entries[0].OldValue.Should().Be("Open");
        result.Entries[0].NewValue.Should().Be("In Progress");
        result.Entries[1].FieldChanged.Should().Be("Title");
    }
}
