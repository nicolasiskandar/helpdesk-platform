using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using TicketService.Application.DTOs;
using TicketService.Application.Events;
using TicketService.Application.Interfaces;
using TicketService.Domain.Entities;
using TicketService.Domain.Interfaces;
using TicketService.Infrastructure.Services;
using Xunit;

namespace TicketService.Tests.Services;

public class TicketBusinessServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IReferenceNumberGenerator> _referenceNumberGeneratorMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly TicketBusinessService _sut;

    private readonly Mock<ITicketRepository> _ticketRepoMock = new();
    private readonly Mock<ICategoryRepository> _categoryRepoMock = new();
    private readonly Mock<IPriorityRepository> _priorityRepoMock = new();
    private readonly Mock<IStatusRepository> _statusRepoMock = new();
    private readonly Mock<ITicketAssignmentRepository> _assignmentRepoMock = new();
    private readonly Mock<ITicketCommentRepository> _commentRepoMock = new();
    private readonly Mock<ITicketAttachmentRepository> _attachmentRepoMock = new();
    private readonly Mock<ICommentAttachmentRepository> _commentAttachmentRepoMock = new();
    private readonly Mock<ITicketAuditLogRepository> _auditLogRepoMock = new();
    private readonly Mock<IOutboxRepository> _outboxRepoMock = new();
    private readonly Mock<IUserLookupService> _userLookupMock = new();

    public TicketBusinessServiceTests()
    {
        _unitOfWorkMock.Setup(u => u.Tickets).Returns(_ticketRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Categories).Returns(_categoryRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Priorities).Returns(_priorityRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Statuses).Returns(_statusRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.TicketAssignments).Returns(_assignmentRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.TicketComments).Returns(_commentRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.TicketAttachments).Returns(_attachmentRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.CommentAttachments).Returns(_commentAttachmentRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.TicketAuditLogs).Returns(_auditLogRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Outbox).Returns(_outboxRepoMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = "Bearer test-token";
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.Setup(a => a.HttpContext).Returns(httpContext);

        _userLookupMock
            .Setup(l => l.GetRolesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        _userLookupMock
            .Setup(l => l.GetUserIdsByRoleAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<Guid>());

        _assignmentRepoMock
            .Setup(r => r.GetByTicketIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<TicketAssignment>());

        _sut = new TicketBusinessService(
            _unitOfWorkMock.Object,
            _referenceNumberGeneratorMock.Object,
            _fileStorageMock.Object,
            _userLookupMock.Object,
            httpContextAccessorMock.Object);
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
    public async Task CreateTicketAsync_Success_EmbedsManagerAndAdminIdsInEvent()
    {
        // Arrange
        var managerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _categoryRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Category { Id = 1, Name = "Hardware" });
        _priorityRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Priority { Id = 1, Name = "Low", Level = 1 });
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });
        _referenceNumberGeneratorMock.Setup(g => g.GenerateAsync()).ReturnsAsync("TKT-000001");
        _userLookupMock
            .Setup(l => l.GetUserIdsByRoleAsync("Manager", It.IsAny<string>()))
            .ReturnsAsync(new List<Guid> { managerId });
        _userLookupMock
            .Setup(l => l.GetUserIdsByRoleAsync("Admin", It.IsAny<string>()))
            .ReturnsAsync(new List<Guid> { adminId });

        OutboxMessage? captured = null;
        _outboxRepoMock
            .Setup(r => r.AddAsync(It.IsAny<OutboxMessage>()))
            .Callback<OutboxMessage>(m => captured = m);

        // Act
        var result = await _sut.CreateTicketAsync(new CreateTicketRequest("Test Title", "Test Description", 1, 1), userId);

        // Assert
        result.ReferenceNumber.Should().Be("TKT-000001");
        captured.Should().NotBeNull();
        captured!.EventType.Should().Be("ticket.created");

        var evt = JsonSerializer.Deserialize<TicketCreatedEvent>(captured.Payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        evt.Should().NotBeNull();
        evt!.ManagerUserIds.Should().Contain(managerId);
        evt.AdminUserIds.Should().Contain(adminId);
    }

    [Fact]
    public async Task CreateTicketAsync_NoAccessToken_DoesNotLookupRoles()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());

        var sut = new TicketBusinessService(
            _unitOfWorkMock.Object,
            _referenceNumberGeneratorMock.Object,
            _fileStorageMock.Object,
            _userLookupMock.Object,
            httpContextAccessorMock.Object);

        _categoryRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Category { Id = 1, Name = "Hardware" });
        _priorityRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Priority { Id = 1, Name = "Low", Level = 1 });
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });
        _referenceNumberGeneratorMock.Setup(g => g.GenerateAsync()).ReturnsAsync("TKT-000001");

        // Act
        await sut.CreateTicketAsync(new CreateTicketRequest("Test Title", "Test Description", 1, 1), userId);

        // Assert
        _userLookupMock.Verify(l => l.GetUserIdsByRoleAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
    public async Task GetTicketByIdAsync_ResolvedTicket_ReturnsTimeWorkedMinutes()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var assignedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var resolvedAt = assignedAt.AddHours(2);

        var ticket = BuildTicket(ticketId, "Resolved - Pending Confirmation", createdBy);
        ticket.CreatedAt = assignedAt;
        ticket.Assignments = new List<TicketAssignment>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, AgentUserId = Guid.NewGuid(), AssignedByUserId = Guid.NewGuid(), AssignedAt = assignedAt, UnassignedAt = null }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _auditLogRepoMock.Setup(r => r.GetStatusTransitionsAsync(ticketId)).ReturnsAsync(new List<TicketAuditLogEntry>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, FieldChanged = "Status", OldValue = "In Progress", NewValue = "Resolved - Pending Confirmation", ChangedAt = resolvedAt }
        });

        // Act
        var result = await _sut.GetTicketByIdAsync(ticketId);

        // Assert
        result.TimeWorkedMinutes.Should().Be(120);
        result.TimeToCloseMinutes.Should().BeNull();
    }

    [Fact]
    public async Task GetTicketByIdAsync_ReassignedTicket_SumsActiveAssignmentTime()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var assignedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var resolvedAt = assignedAt.AddHours(4);

        var ticket = BuildTicket(ticketId, "Resolved - Pending Confirmation", createdBy);
        ticket.Assignments = new List<TicketAssignment>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, AgentUserId = Guid.NewGuid(), AssignedByUserId = Guid.NewGuid(), AssignedAt = assignedAt, UnassignedAt = assignedAt.AddHours(1) },
            new() { Id = Guid.NewGuid(), TicketId = ticketId, AgentUserId = Guid.NewGuid(), AssignedByUserId = Guid.NewGuid(), AssignedAt = assignedAt.AddHours(3), UnassignedAt = null }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _auditLogRepoMock.Setup(r => r.GetStatusTransitionsAsync(ticketId)).ReturnsAsync(new List<TicketAuditLogEntry>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, FieldChanged = "Status", OldValue = "In Progress", NewValue = "Resolved - Pending Confirmation", ChangedAt = resolvedAt }
        });

        // Act
        var result = await _sut.GetTicketByIdAsync(ticketId);

        // Assert
        result.TimeWorkedMinutes.Should().Be(120);
    }

    [Fact]
    public async Task GetTicketByIdAsync_ClosedTicket_ReturnsTimeWorkedAndTimeToClose()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var createdAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var resolvedAt = createdAt.AddHours(2);
        var closedAt = createdAt.AddHours(5);

        var ticket = BuildTicket(ticketId, "Closed", createdBy);
        ticket.CreatedAt = createdAt;
        ticket.Assignments = new List<TicketAssignment>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, AgentUserId = Guid.NewGuid(), AssignedByUserId = Guid.NewGuid(), AssignedAt = createdAt, UnassignedAt = null }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _auditLogRepoMock.Setup(r => r.GetStatusTransitionsAsync(ticketId)).ReturnsAsync(new List<TicketAuditLogEntry>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, FieldChanged = "Status", OldValue = "In Progress", NewValue = "Resolved - Pending Confirmation", ChangedAt = resolvedAt },
            new() { Id = Guid.NewGuid(), TicketId = ticketId, FieldChanged = "Status", OldValue = "Resolved - Pending Confirmation", NewValue = "Closed", ChangedAt = closedAt }
        });

        // Act
        var result = await _sut.GetTicketByIdAsync(ticketId);

        // Assert
        result.TimeWorkedMinutes.Should().Be(120);
        result.TimeToCloseMinutes.Should().Be(300);
    }

    [Fact]
    public async Task GetTicketByIdAsync_UnresolvedTicket_ReturnsNullMetrics()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        var ticket = BuildTicket(ticketId, "In Progress", createdBy);
        ticket.Assignments = new List<TicketAssignment>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, AgentUserId = Guid.NewGuid(), AssignedByUserId = Guid.NewGuid(), AssignedAt = DateTime.UtcNow, UnassignedAt = null }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _auditLogRepoMock.Setup(r => r.GetStatusTransitionsAsync(ticketId)).ReturnsAsync(new List<TicketAuditLogEntry>());

        // Act
        var result = await _sut.GetTicketByIdAsync(ticketId);

        // Assert
        result.TimeWorkedMinutes.Should().BeNull();
        result.TimeToCloseMinutes.Should().BeNull();
    }

    private static Ticket BuildTicket(Guid ticketId, string statusName, Guid createdByUserId)
    {
        return new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = createdByUserId,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = statusName }
        };
    }

    [Fact]
    public async Task EnsureTicketAccessAsync_PendingTicket_AdminAllowed()
    {
        var ticket = BuildTicket(Guid.NewGuid(), "Resolved - Pending Confirmation", Guid.NewGuid());
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticket.Id)).ReturnsAsync(ticket);

        var act = () => _sut.EnsureTicketAccessAsync(ticket.Id, Guid.NewGuid(), "Admin");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureTicketAccessAsync_PendingTicket_ManagerAllowed()
    {
        var ticket = BuildTicket(Guid.NewGuid(), "Resolved - Pending Confirmation", Guid.NewGuid());
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticket.Id)).ReturnsAsync(ticket);

        var act = () => _sut.EnsureTicketAccessAsync(ticket.Id, Guid.NewGuid(), "Manager");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureTicketAccessAsync_PendingTicket_CreatorAllowed()
    {
        var createdBy = Guid.NewGuid();
        var ticket = BuildTicket(Guid.NewGuid(), "In Progress", createdBy);
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticket.Id)).ReturnsAsync(ticket);

        var act = () => _sut.EnsureTicketAccessAsync(ticket.Id, createdBy, "Employee");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureTicketAccessAsync_PendingTicket_AssignedAgentAllowed()
    {
        var agentId = Guid.NewGuid();
        var ticket = BuildTicket(Guid.NewGuid(), "In Progress", Guid.NewGuid());
        ticket.Assignments.Add(new TicketAssignment { Id = Guid.NewGuid(), TicketId = ticket.Id, AgentUserId = agentId, AssignedAt = DateTime.UtcNow });
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticket.Id)).ReturnsAsync(ticket);

        var act = () => _sut.EnsureTicketAccessAsync(ticket.Id, agentId, "IT Support Agent");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureTicketAccessAsync_PendingTicket_UnassignedAgentDenied()
    {
        var agentId = Guid.NewGuid();
        var ticket = BuildTicket(Guid.NewGuid(), "In Progress", Guid.NewGuid());
        ticket.Assignments.Add(new TicketAssignment
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            AgentUserId = agentId,
            AssignedAt = DateTime.UtcNow,
            UnassignedAt = DateTime.UtcNow
        });
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticket.Id)).ReturnsAsync(ticket);

        var act = () => _sut.EnsureTicketAccessAsync(ticket.Id, agentId, "IT Support Agent");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*do not have access*");
    }

    [Fact]
    public async Task EnsureTicketAccessAsync_PendingTicket_UnrelatedUserDenied()
    {
        var ticket = BuildTicket(Guid.NewGuid(), "Resolved - Pending Confirmation", Guid.NewGuid());
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticket.Id)).ReturnsAsync(ticket);

        var act = () => _sut.EnsureTicketAccessAsync(ticket.Id, Guid.NewGuid(), "Employee");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*do not have access*");
    }

    [Fact]
    public async Task EnsureTicketAccessAsync_OpenTicket_AnyUserAllowed()
    {
        var ticket = BuildTicket(Guid.NewGuid(), "Open", Guid.NewGuid());
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticket.Id)).ReturnsAsync(ticket);

        var act = () => _sut.EnsureTicketAccessAsync(ticket.Id, Guid.NewGuid(), "Employee");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureTicketAccessAsync_ClosedTicket_AnyUserAllowed()
    {
        var ticket = BuildTicket(Guid.NewGuid(), "Closed", Guid.NewGuid());
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticket.Id)).ReturnsAsync(ticket);

        var act = () => _sut.EnsureTicketAccessAsync(ticket.Id, Guid.NewGuid(), "Employee");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureTicketAccessAsync_TicketNotFound_ReturnsSilently()
    {
        var ticketId = Guid.NewGuid();
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync((Ticket?)null);

        var act = () => _sut.EnsureTicketAccessAsync(ticketId, Guid.NewGuid(), "Employee");

        await act.Should().NotThrowAsync();
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
        _statusRepoMock.Setup(r => r.GetByNameAsync("In Progress")).ReturnsAsync(new Status { Id = 2, Name = "In Progress" });

        // Act
        var result = await _sut.AssignAgentAsync(ticketId, new AssignAgentRequest(agentUserId), assignedByUserId, "Admin User");

        // Assert
        result.AgentUserId.Should().Be(agentUserId);
        result.AssignedByUserId.Should().Be(assignedByUserId);
        result.UnassignedAt.Should().BeNull();

        ticket.StatusId.Should().Be(2);

        _assignmentRepoMock.Verify(r => r.AddAsync(It.Is<TicketAssignment>(a =>
            a.TicketId == ticketId &&
            a.AgentUserId == agentUserId &&
            a.AssignedByUserId == assignedByUserId
        )), Times.Once);

        _auditLogRepoMock.Verify(r => r.AddAsync(It.Is<TicketAuditLogEntry>(a =>
            a.FieldChanged == "Status" && a.NewValue == "In Progress"
        )), Times.Once);

        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.assigned"
        )), Times.Once);

        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.status_changed"
        )), Times.Once);
    }

    [Fact]
    public async Task AssignAgentAsync_TicketNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync((Ticket?)null);

        // Act
        var act = () => _sut.AssignAgentAsync(ticketId, new AssignAgentRequest(Guid.NewGuid()), Guid.NewGuid(), "Admin User");

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
        var act = () => _sut.AssignAgentAsync(ticketId, new AssignAgentRequest(agentUserId), Guid.NewGuid(), "Admin User");

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
    public async Task ChangeStatusAsync_ToClosed_AddsTicketClosedOutboxMessage()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 3,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 3, Name = "Resolved - Pending Confirmation" }
        };

        var newStatus = new Status { Id = 4, Name = "Closed" };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByIdAsync(4)).ReturnsAsync(newStatus);
        _userLookupMock
            .Setup(l => l.GetUserIdsByRoleAsync("Admin", It.IsAny<string>()))
            .ReturnsAsync(new List<Guid> { adminId });

        var addedMessages = new List<OutboxMessage>();
        _outboxRepoMock
            .Setup(r => r.AddAsync(It.IsAny<OutboxMessage>()))
            .Callback<OutboxMessage>(m => addedMessages.Add(m));

        // Act
        var result = await _sut.ChangeStatusAsync(ticketId, new ChangeStatusRequest(4, null), userId);

        // Assert
        result.StatusName.Should().Be("Closed");

        var closedMessage = addedMessages.Single(m => m.EventType == "ticket.closed");
        var evt = JsonSerializer.Deserialize<TicketClosedEvent>(closedMessage.Payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        evt.Should().NotBeNull();
        evt!.AdminUserIds.Should().Contain(adminId);
        evt.ClosedByUserId.Should().Be(userId);
        evt.ReferenceNumber.Should().Be("TKT-000001");
    }

    [Fact]
    public async Task ChangeStatusAsync_NonClose_DoesNotAddTicketClosedOutboxMessage()
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
        await _sut.ChangeStatusAsync(ticketId, new ChangeStatusRequest(2, null), userId);

        // Assert
        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.closed"
        )), Times.Never);
    }

    [Fact]
    public async Task ChangeStatusAsync_EmbedsCreatorAndActiveAssigneesInEvent()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var changedById = Guid.NewGuid();

        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = creatorId,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        var newStatus = new Status { Id = 2, Name = "In Progress" };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(newStatus);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>
        {
            new() { TicketId = ticketId, AgentUserId = agentId, UnassignedAt = null }
        });

        var captured = new List<OutboxMessage>();
        _outboxRepoMock
            .Setup(r => r.AddAsync(It.IsAny<OutboxMessage>()))
            .Callback<OutboxMessage>(m => captured.Add(m));

        // Act
        await _sut.ChangeStatusAsync(ticketId, new ChangeStatusRequest(2, null), changedById);

        // Assert
        var statusChanged = captured.Single(m => m.EventType == "ticket.status_changed");
        var evt = JsonSerializer.Deserialize<TicketStatusChangedEvent>(statusChanged.Payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        evt.Should().NotBeNull();
        evt!.RecipientUserIds.Should().Contain(creatorId);
        evt.RecipientUserIds.Should().Contain(agentId);
        evt.RecipientUserIds.Should().NotContain(changedById);
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
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());

        // Act
        var result = await _sut.AddCommentAsync(ticketId, new AddCommentRequest("Test comment", false), authorUserId, "Employee", "Test User");

        // Assert
        result.Content.Should().Be("Test comment");
        result.IsPrivate.Should().BeFalse();
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

    [Theory]
    [InlineData(2, "In Progress")]
    [InlineData(3, "Resolved - Pending Confirmation")]
    [InlineData(4, "Closed")]
    [InlineData(5, "Resolved by AI")]
    public async Task UpdateTicketAsync_NonOpenStatus_ThrowsInvalidOperationException(int statusId, string statusName)
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
            StatusId = statusId,
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = statusId, Name = statusName }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });

        var request = new UpdateTicketRequest("New Title", null, null, null);

        // Act
        var act = () => _sut.UpdateTicketAsync(ticketId, request, Guid.NewGuid(), "Admin");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only open tickets can be edited*");

        _auditLogRepoMock.Verify(r => r.AddAsync(It.IsAny<TicketAuditLogEntry>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateTicketAsync_OpenTicket_CreatorCanEdit()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Title",
            Description = "Description",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });

        var request = new UpdateTicketRequest("Updated Title", null, null, null);

        // Act
        var result = await _sut.UpdateTicketAsync(ticketId, request, userId, "Employee");

        // Assert
        result.Title.Should().Be("Updated Title");
        _auditLogRepoMock.Verify(r => r.AddAsync(It.Is<TicketAuditLogEntry>(e =>
            e.FieldChanged == "Title" &&
            e.NewValue == "Updated Title"
        )), Times.Once);
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
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment> { assignment });

        // Act
        await _sut.UnassignAgentAsync(ticketId, new UnassignAgentRequest(agentUserId), changedByUserId, "Admin User");

        // Assert
        assignment.UnassignedAt.Should().NotBeNull();
        assignment.UnassignedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _assignmentRepoMock.Verify(r => r.UpdateAsync(assignment), Times.Once);

        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.unassigned"
        )), Times.Once);

        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.status_changed"
        )), Times.Never);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnassignAgentAsync_LastAgent_InProgressTransitionsToOpen()
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
            StatusId = 2,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 2, Name = "In Progress" }
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
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment> { assignment });
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });

        // Act
        await _sut.UnassignAgentAsync(ticketId, new UnassignAgentRequest(agentUserId), changedByUserId, "Admin User");

        // Assert
        ticket.StatusId.Should().Be(1);

        _auditLogRepoMock.Verify(r => r.AddAsync(It.Is<TicketAuditLogEntry>(a =>
            a.FieldChanged == "Status" && a.NewValue == "Open"
        )), Times.Once);

        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.status_changed"
        )), Times.Once);

        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.unassigned"
        )), Times.Once);
    }

    [Fact]
    public async Task UnassignAgentAsync_TicketNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync((Ticket?)null);

        // Act
        var act = () => _sut.UnassignAgentAsync(ticketId, new UnassignAgentRequest(Guid.NewGuid()), Guid.NewGuid(), "Admin User");

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
        var act = () => _sut.UnassignAgentAsync(ticketId, new UnassignAgentRequest(agentUserId), Guid.NewGuid(), "Admin User");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Assignment not found*");
    }

    [Fact]
    public async Task EscalateTicketAsync_Success_UnassignsAgentAndReturnsToOpen()
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
            StatusId = 2,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 2, Name = "In Progress" }
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
        _statusRepoMock.Setup(r => r.GetByNameAsync("In Progress")).ReturnsAsync(new Status { Id = 2, Name = "In Progress" });
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });
        _assignmentRepoMock.Setup(r => r.GetActiveAssignmentAsync(ticketId, agentUserId)).ReturnsAsync(assignment);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment> { assignment });

        // Act
        var result = await _sut.EscalateTicketAsync(ticketId, agentUserId, "Agent User", "Cannot reproduce issue");

        // Assert
        result.StatusName.Should().Be("Open");
        assignment.UnassignedAt.Should().NotBeNull();

        _assignmentRepoMock.Verify(r => r.UpdateAsync(assignment), Times.Once);

        _auditLogRepoMock.Verify(r => r.AddAsync(It.Is<TicketAuditLogEntry>(a =>
            a.FieldChanged == "Assignment" && a.NewValue == "Escalated: Cannot reproduce issue"
        )), Times.Once);

        _auditLogRepoMock.Verify(r => r.AddAsync(It.Is<TicketAuditLogEntry>(a =>
            a.FieldChanged == "Status" && a.OldValue == "In Progress" && a.NewValue == "Open"
        )), Times.Once);

        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.unassigned"
        )), Times.Once);

        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.status_changed"
        )), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EscalateTicketAsync_NoReason_UsesDefaultAuditMessage()
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
            StatusId = 2,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 2, Name = "In Progress" }
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
        _statusRepoMock.Setup(r => r.GetByNameAsync("In Progress")).ReturnsAsync(new Status { Id = 2, Name = "In Progress" });
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });
        _assignmentRepoMock.Setup(r => r.GetActiveAssignmentAsync(ticketId, agentUserId)).ReturnsAsync(assignment);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment> { assignment });

        // Act
        await _sut.EscalateTicketAsync(ticketId, agentUserId, "Agent User", null);

        // Assert
        _auditLogRepoMock.Verify(r => r.AddAsync(It.Is<TicketAuditLogEntry>(a =>
            a.FieldChanged == "Assignment" && a.NewValue == "Escalated"
        )), Times.Once);
    }

    [Fact]
    public async Task EscalateTicketAsync_TicketNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync((Ticket?)null);

        // Act
        var act = () => _sut.EscalateTicketAsync(ticketId, Guid.NewGuid(), "Agent User", null);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Ticket not found*");
    }

    [Fact]
    public async Task EscalateTicketAsync_NotInProgress_ThrowsInvalidOperationException()
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
            StatusId = 4,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 4, Name = "Closed" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByNameAsync("In Progress")).ReturnsAsync(new Status { Id = 2, Name = "In Progress" });

        // Act
        var act = () => _sut.EscalateTicketAsync(ticketId, agentUserId, "Agent User", null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only tickets in progress can be escalated*");
    }

    [Fact]
    public async Task EscalateTicketAsync_NotAssigned_ThrowsInvalidOperationException()
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
            StatusId = 2,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 2, Name = "In Progress" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByNameAsync("In Progress")).ReturnsAsync(new Status { Id = 2, Name = "In Progress" });
        _assignmentRepoMock.Setup(r => r.GetActiveAssignmentAsync(ticketId, agentUserId)).ReturnsAsync((TicketAssignment?)null);

        // Act
        var act = () => _sut.EscalateTicketAsync(ticketId, agentUserId, "Agent User", null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not assigned*");
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
    public async Task UploadAttachmentAsync_Success_CapturesSizeAndSavesFile()
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
        _fileStorageMock.Setup(f => f.SaveFileAsync(It.IsAny<Stream>(), "photo.png", ticketId.ToString()))
            .ReturnsAsync($"{ticketId}/stored.png");

        using var stream = new MemoryStream(new byte[1234]);

        // Act
        var result = await _sut.UploadAttachmentAsync(ticketId, stream, "photo.png", userId, "Admin");

        // Assert
        result.FileName.Should().Be("photo.png");
        result.Size.Should().Be(1234);
        _fileStorageMock.Verify(f => f.SaveFileAsync(stream, "photo.png", ticketId.ToString()), Times.Once);
        _attachmentRepoMock.Verify(r => r.AddAsync(It.Is<TicketAttachment>(a => a.Size == 1234)), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadAttachmentAsync_DisallowedExtension_ThrowsInvalidOperationException()
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

        using var stream = new MemoryStream(new byte[10]);

        // Act
        var act = () => _sut.UploadAttachmentAsync(ticketId, stream, "malware.exe", userId, "Admin");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not allowed*");
        _fileStorageMock.Verify(f => f.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _attachmentRepoMock.Verify(r => r.AddAsync(It.IsAny<TicketAttachment>()), Times.Never);
    }

    [Fact]
    public async Task UploadAttachmentAsync_Admin_CanUploadWhenTicketNotOpen()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var ticket = BuildTicket(ticketId, "Closed", creatorId);

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _fileStorageMock.Setup(f => f.SaveFileAsync(It.IsAny<Stream>(), "photo.png", ticketId.ToString()))
            .ReturnsAsync($"{ticketId}/stored.png");

        using var stream = new MemoryStream(new byte[10]);

        // Act
        var result = await _sut.UploadAttachmentAsync(ticketId, stream, "photo.png", adminId, "Admin");

        // Assert
        result.FileName.Should().Be("photo.png");
        _fileStorageMock.Verify(f => f.SaveFileAsync(It.IsAny<Stream>(), "photo.png", ticketId.ToString()), Times.Once);
    }

    [Fact]
    public async Task UploadAttachmentAsync_EmployeeCreator_CanUploadWhenOpen()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var ticket = BuildTicket(ticketId, "Open", creatorId);

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _fileStorageMock.Setup(f => f.SaveFileAsync(It.IsAny<Stream>(), "photo.png", ticketId.ToString()))
            .ReturnsAsync($"{ticketId}/stored.png");

        using var stream = new MemoryStream(new byte[10]);

        // Act
        var result = await _sut.UploadAttachmentAsync(ticketId, stream, "photo.png", creatorId, "Employee");

        // Assert
        result.FileName.Should().Be("photo.png");
        _fileStorageMock.Verify(f => f.SaveFileAsync(It.IsAny<Stream>(), "photo.png", ticketId.ToString()), Times.Once);
    }

    [Fact]
    public async Task UploadAttachmentAsync_EmployeeCreator_CannotUploadWhenNotOpen()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var ticket = BuildTicket(ticketId, "In Progress", creatorId);

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);

        using var stream = new MemoryStream(new byte[10]);

        // Act
        var act = () => _sut.UploadAttachmentAsync(ticketId, stream, "photo.png", creatorId, "Employee");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _fileStorageMock.Verify(f => f.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadAttachmentAsync_Manager_CannotUpload()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var ticket = BuildTicket(ticketId, "Open", managerId);

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);

        using var stream = new MemoryStream(new byte[10]);

        // Act
        var act = () => _sut.UploadAttachmentAsync(ticketId, stream, "photo.png", managerId, "Manager");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _fileStorageMock.Verify(f => f.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadAttachmentAsync_NonCreatorEmployee_CannotUpload()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var otherEmployeeId = Guid.NewGuid();
        var ticket = BuildTicket(ticketId, "Open", creatorId);

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);

        using var stream = new MemoryStream(new byte[10]);

        // Act
        var act = () => _sut.UploadAttachmentAsync(ticketId, stream, "photo.png", otherEmployeeId, "Employee");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _fileStorageMock.Verify(f => f.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadAttachmentAsync_Agent_CannotUpload()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var ticket = BuildTicket(ticketId, "Open", creatorId);

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);

        using var stream = new MemoryStream(new byte[10]);

        // Act
        var act = () => _sut.UploadAttachmentAsync(ticketId, stream, "photo.png", agentId, "IT Support Agent");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _fileStorageMock.Verify(f => f.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAttachmentAsync_Success_DeletesFileRowAndWritesAudit()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = uploaderId,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };
        var attachment = new TicketAttachment
        {
            Id = attachmentId,
            TicketId = ticketId,
            FileName = "photo.png",
            FileUrl = $"{ticketId}/stored.png",
            UploadedByUserId = uploaderId,
            UploadedAt = DateTime.UtcNow
        };
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _attachmentRepoMock.Setup(r => r.GetByIdAsync(attachmentId)).ReturnsAsync(attachment);

        // Act
        await _sut.DeleteAttachmentAsync(ticketId, attachmentId, uploaderId, "Employee");

        // Assert
        _fileStorageMock.Verify(f => f.DeleteFileAsync($"{ticketId}/stored.png"), Times.Once);
        _attachmentRepoMock.Verify(r => r.DeleteAsync(attachment), Times.Once);
        _auditLogRepoMock.Verify(r => r.AddAsync(It.Is<TicketAuditLogEntry>(e =>
            e.FieldChanged == "Attachment" && e.NewValue == "Attachment deleted")), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAttachmentAsync_AdminCanDeleteAnyAttachment()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = Guid.NewGuid(),
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };
        var attachment = new TicketAttachment
        {
            Id = attachmentId,
            TicketId = ticketId,
            FileName = "photo.png",
            FileUrl = $"{ticketId}/stored.png",
            UploadedByUserId = Guid.NewGuid()
        };
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _attachmentRepoMock.Setup(r => r.GetByIdAsync(attachmentId)).ReturnsAsync(attachment);

        // Act
        await _sut.DeleteAttachmentAsync(ticketId, attachmentId, adminId, "Admin");

        // Assert
        _fileStorageMock.Verify(f => f.DeleteFileAsync($"{ticketId}/stored.png"), Times.Once);
        _attachmentRepoMock.Verify(r => r.DeleteAsync(attachment), Times.Once);
    }

    [Fact]
    public async Task DeleteAttachmentAsync_NoPermission_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = Guid.NewGuid(),
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };
        var attachment = new TicketAttachment
        {
            Id = attachmentId,
            TicketId = ticketId,
            FileName = "photo.png",
            FileUrl = $"{ticketId}/stored.png",
            UploadedByUserId = Guid.NewGuid()
        };
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _attachmentRepoMock.Setup(r => r.GetByIdAsync(attachmentId)).ReturnsAsync(attachment);

        // Act
        var act = () => _sut.DeleteAttachmentAsync(ticketId, attachmentId, otherUserId, "Employee");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _fileStorageMock.Verify(f => f.DeleteFileAsync(It.IsAny<string>()), Times.Never);
        _attachmentRepoMock.Verify(r => r.DeleteAsync(It.IsAny<TicketAttachment>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAttachmentAsync_EmployeeCreator_CannotDeleteWhenNotOpen()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var ticket = BuildTicket(ticketId, "Closed", creatorId);
        var attachment = new TicketAttachment
        {
            Id = attachmentId,
            TicketId = ticketId,
            FileName = "photo.png",
            FileUrl = $"{ticketId}/stored.png",
            UploadedByUserId = creatorId
        };
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _attachmentRepoMock.Setup(r => r.GetByIdAsync(attachmentId)).ReturnsAsync(attachment);

        // Act
        var act = () => _sut.DeleteAttachmentAsync(ticketId, attachmentId, creatorId, "Employee");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _fileStorageMock.Verify(f => f.DeleteFileAsync(It.IsAny<string>()), Times.Never);
        _attachmentRepoMock.Verify(r => r.DeleteAsync(It.IsAny<TicketAttachment>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAttachmentAsync_Manager_CannotDelete()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var ticket = BuildTicket(ticketId, "Open", Guid.NewGuid());
        var attachment = new TicketAttachment
        {
            Id = attachmentId,
            TicketId = ticketId,
            FileName = "photo.png",
            FileUrl = $"{ticketId}/stored.png",
            UploadedByUserId = Guid.NewGuid()
        };
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _attachmentRepoMock.Setup(r => r.GetByIdAsync(attachmentId)).ReturnsAsync(attachment);

        // Act
        var act = () => _sut.DeleteAttachmentAsync(ticketId, attachmentId, managerId, "Manager");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _fileStorageMock.Verify(f => f.DeleteFileAsync(It.IsAny<string>()), Times.Never);
        _attachmentRepoMock.Verify(r => r.DeleteAsync(It.IsAny<TicketAttachment>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAttachmentAsync_Agent_CannotDelete_EvenWhenUploader()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var ticket = BuildTicket(ticketId, "Open", Guid.NewGuid());
        var attachment = new TicketAttachment
        {
            Id = attachmentId,
            TicketId = ticketId,
            FileName = "photo.png",
            FileUrl = $"{ticketId}/stored.png",
            UploadedByUserId = agentId
        };
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _attachmentRepoMock.Setup(r => r.GetByIdAsync(attachmentId)).ReturnsAsync(attachment);

        // Act
        var act = () => _sut.DeleteAttachmentAsync(ticketId, attachmentId, agentId, "IT Support Agent");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _fileStorageMock.Verify(f => f.DeleteFileAsync(It.IsAny<string>()), Times.Never);
        _attachmentRepoMock.Verify(r => r.DeleteAsync(It.IsAny<TicketAttachment>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAttachmentAsync_NonCreatorEmployeeUploader_CannotDelete()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var ticket = BuildTicket(ticketId, "Open", Guid.NewGuid());
        var attachment = new TicketAttachment
        {
            Id = attachmentId,
            TicketId = ticketId,
            FileName = "photo.png",
            FileUrl = $"{ticketId}/stored.png",
            UploadedByUserId = uploaderId
        };
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _attachmentRepoMock.Setup(r => r.GetByIdAsync(attachmentId)).ReturnsAsync(attachment);

        // Act
        var act = () => _sut.DeleteAttachmentAsync(ticketId, attachmentId, uploaderId, "Employee");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _fileStorageMock.Verify(f => f.DeleteFileAsync(It.IsAny<string>()), Times.Never);
        _attachmentRepoMock.Verify(r => r.DeleteAsync(It.IsAny<TicketAttachment>()), Times.Never);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsAggregatesForWindow()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var firstOfMonth = new DateTime(now.Year, now.Month, 1);
        var from = firstOfMonth.AddMonths(-5);
        var ticketId1 = Guid.NewGuid();
        var ticketId2 = Guid.NewGuid();
        var tickets = new List<Ticket>
        {
            new() { Id = ticketId1, CreatedAt = firstOfMonth.AddDays(1), StatusId = 1, PriorityId = 4 },
            new() { Id = ticketId2, CreatedAt = firstOfMonth.AddDays(2), StatusId = 4, PriorityId = 1 }
        };
        var transitions = new List<TicketAuditLogEntry>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId2, NewValue = "Closed", ChangedAt = firstOfMonth.AddDays(3) }
        };
        _ticketRepoMock.Setup(r => r.GetForAnalyticsAsync(from, It.IsAny<DateTime>())).ReturnsAsync(tickets);
        _ticketRepoMock.Setup(r => r.GetUnassignedCountAsync(from, It.IsAny<DateTime>())).ReturnsAsync(1);
        _auditLogRepoMock.Setup(r => r.GetResolutionTransitionsAsync(from, It.IsAny<DateTime>())).ReturnsAsync(transitions);

        // Act
        var result = await _sut.GetStatisticsAsync();

        // Assert
        result.Overview.Total.Should().Be(2);
        result.Overview.Open.Should().Be(1);
        result.Overview.Resolved.Should().Be(1);
        result.Overview.CriticalOpen.Should().Be(1);
        result.Overview.Unassigned.Should().Be(1);
        result.Overview.ResolutionRate.Should().Be(50.0);
        result.Overview.SlaCompliance.Should().Be(100.0);
        result.VolumeTrend.Should().HaveCount(6);
        result.VolumeTrend[^1].Created.Should().Be(2);
        result.VolumeTrend[^1].Resolved.Should().Be(1);
        result.ResolutionTrend.Should().HaveCount(6);
        result.ResolutionTrend[^1].AverageHours.Should().BeApproximately(24.0, 0.1);
    }

    [Fact]
    public async Task GetStatisticsAsync_NoData_ReturnsEmptyOverview()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var firstOfMonth = new DateTime(now.Year, now.Month, 1);
        var from = firstOfMonth.AddMonths(-5);
        _ticketRepoMock.Setup(r => r.GetForAnalyticsAsync(from, It.IsAny<DateTime>())).ReturnsAsync(new List<Ticket>());
        _ticketRepoMock.Setup(r => r.GetUnassignedCountAsync(from, It.IsAny<DateTime>())).ReturnsAsync(0);
        _auditLogRepoMock.Setup(r => r.GetResolutionTransitionsAsync(from, It.IsAny<DateTime>())).ReturnsAsync(new List<TicketAuditLogEntry>());

        // Act
        var result = await _sut.GetStatisticsAsync();

        // Assert
        result.Overview.Total.Should().Be(0);
        result.Overview.ResolutionRate.Should().BeNull();
        result.Overview.AverageResolutionHours.Should().BeNull();
        result.Overview.SlaCompliance.Should().BeNull();
        result.VolumeTrend.Should().HaveCount(6);
        result.VolumeTrend[^1].Created.Should().Be(0);
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
    public async Task ChangeStatusAsync_ValidTransition_InProgressToPendingConfirmation_Succeeds()
    {
        var ticketId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId, ReferenceNumber = "TKT-000001", Title = "Test",
            CategoryId = 1, PriorityId = 1, StatusId = 2,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 2, Name = "In Progress" }
        };
        var newStatus = new Status { Id = 3, Name = "Resolved - Pending Confirmation" };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(newStatus);

        var result = await _sut.ChangeStatusAsync(ticketId, new ChangeStatusRequest(3, null), userId);

        result.StatusName.Should().Be("Resolved - Pending Confirmation");
    }

    [Fact]
    public async Task ChangeStatusAsync_ValidTransition_PendingToClosed_Succeeds()
    {
        var ticketId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId, ReferenceNumber = "TKT-000001", Title = "Test",
            CategoryId = 1, PriorityId = 1, StatusId = 3,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 3, Name = "Resolved - Pending Confirmation" }
        };
        var newStatus = new Status { Id = 4, Name = "Closed" };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByIdAsync(4)).ReturnsAsync(newStatus);

        var result = await _sut.ChangeStatusAsync(ticketId, new ChangeStatusRequest(4, null), userId);

        result.StatusName.Should().Be("Closed");
    }

    [Fact]
    public async Task ChangeStatusAsync_InvalidTransition_OpenToClosed_ThrowsInvalidOperationException()
    {
        var ticketId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId, ReferenceNumber = "TKT-000001", Title = "Test",
            CategoryId = 1, PriorityId = 1, StatusId = 1,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };
        var closedStatus = new Status { Id = 4, Name = "Closed" };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByIdAsync(4)).ReturnsAsync(closedStatus);

        var act = () => _sut.ChangeStatusAsync(ticketId, new ChangeStatusRequest(4, null), Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot transition*");
    }

    [Fact]
    public async Task ChangeStatusAsync_TerminalState_ClosedToInProgress_ThrowsInvalidOperationException()
    {
        var ticketId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId, ReferenceNumber = "TKT-000001", Title = "Test",
            CategoryId = 1, PriorityId = 1, StatusId = 4,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 4, Name = "Closed" }
        };
        var inProgressStatus = new Status { Id = 2, Name = "In Progress" };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(inProgressStatus);

        var act = () => _sut.ChangeStatusAsync(ticketId, new ChangeStatusRequest(2, null), Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot change status*");
    }

    [Fact]
    public async Task ChangeStatusAsync_ValidTransition_PendingToInProgress_Succeeds()
    {
        var ticketId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId, ReferenceNumber = "TKT-000001", Title = "Test",
            CategoryId = 1, PriorityId = 1, StatusId = 3,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 3, Name = "Resolved - Pending Confirmation" }
        };
        var newStatus = new Status { Id = 2, Name = "In Progress" };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(newStatus);

        var result = await _sut.ChangeStatusAsync(ticketId, new ChangeStatusRequest(2, null), userId);

        result.StatusName.Should().Be("In Progress");
    }

    [Fact]
    public async Task AddCommentAsync_TicketNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync((Ticket?)null);

        // Act
        var act = () => _sut.AddCommentAsync(ticketId, new AddCommentRequest("Comment", false), Guid.NewGuid(), "Employee", "Test User");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Ticket not found*");
    }

    [Fact]
    public async Task AddCommentAsync_PrivateComment_UnauthorizedUser_ThrowsInvalidOperationException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var authorUserId = Guid.NewGuid();
        var ticketCreatorUserId = Guid.NewGuid();

        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = ticketCreatorUserId,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());

        // Act - Employee who is not creator and not assigned tries to create private comment
        var act = () => _sut.AddCommentAsync(ticketId, new AddCommentRequest("Private comment", true), authorUserId, "Employee", "Test User");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*permission*");
    }

    [Fact]
    public async Task AddCommentAsync_ReplyToComment_NotifiesParentAuthorWithoutRestrictingVisibility()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var ticketCreatorUserId = Guid.NewGuid();
        var parentAuthorId = Guid.NewGuid();

        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = ticketCreatorUserId,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        var parentComment = new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorUserId = parentAuthorId,
            Content = "Parent",
            IsPrivate = false
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());
        _commentRepoMock.Setup(r => r.GetByIdAsync(parentComment.Id)).ReturnsAsync(parentComment);

        // Act
        var result = await _sut.AddCommentAsync(
            ticketId,
            new AddCommentRequest("Reply", false, ParentCommentId: parentComment.Id),
            ticketCreatorUserId,
            "Employee",
            "Creator Name");

        // Assert - reply stays public, parent author is notified but visibility is not restricted
        result.ParentCommentId.Should().Be(parentComment.Id);
        result.IsPrivate.Should().BeFalse();
        result.RecipientUserIds.Should().BeEmpty();

        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.commented" &&
            JsonSerializer.Deserialize<TicketCommentedEvent>(m.Payload, (JsonSerializerOptions?)null)!.RecipientUserIds!.Contains(parentAuthorId)
        )), Times.Once);
    }

    [Fact]
    public async Task AddCommentAsync_TargetedComment_StoresRecipientsAndPublishesEvent()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var ticketCreatorUserId = Guid.NewGuid();
        var recipient1 = Guid.NewGuid();
        var recipient2 = Guid.NewGuid();

        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = ticketCreatorUserId,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());
        _userLookupMock
            .Setup(l => l.GetRolesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<Guid, string>
            {
                [recipient1] = "IT Support Agent",
                [recipient2] = "Manager"
            });

        // Act
        var result = await _sut.AddCommentAsync(
            ticketId,
            new AddCommentRequest("Targeted", false, RecipientUserIds: new[] { recipient1, recipient2 }),
            ticketCreatorUserId,
            "Employee",
            "Creator Name");

        // Assert - targeted comments are private and visible only to the chosen recipients
        result.IsPrivate.Should().BeTrue();
        result.RecipientUserIds.Should().BeEquivalentTo(new[] { recipient1, recipient2 });

        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.commented" &&
            JsonSerializer.Deserialize<TicketCommentedEvent>(m.Payload, (JsonSerializerOptions?)null)!.RecipientUserIds!.OrderBy(x => x)
                .SequenceEqual(new[] { recipient1, recipient2 }.OrderBy(x => x))
        )), Times.Once);
    }

    [Fact]
    public async Task AddCommentAsync_TargetedRecipient_Creator_Allowed()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var ticket = BuildTicket(ticketId, "Open", creatorId);
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());
        _userLookupMock
            .Setup(l => l.GetRolesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [creatorId] = "Employee" });

        // Act - admin targets the ticket creator (an employee, but always a valid recipient)
        var result = await _sut.AddCommentAsync(
            ticketId,
            new AddCommentRequest("To creator", false, RecipientUserIds: new[] { creatorId }),
            adminId,
            "Admin",
            "Admin Name");

        // Assert
        result.IsPrivate.Should().BeTrue();
        result.RecipientUserIds.Should().BeEquivalentTo(new[] { creatorId });
    }

    [Fact]
    public async Task AddCommentAsync_TargetedRecipient_Agent_Allowed()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var ticket = BuildTicket(ticketId, "Open", creatorId);
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());
        _userLookupMock
            .Setup(l => l.GetRolesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [agentId] = "IT Support Agent" });

        // Act
        var result = await _sut.AddCommentAsync(
            ticketId,
            new AddCommentRequest("To agent", false, RecipientUserIds: new[] { agentId }),
            adminId,
            "Admin",
            "Admin Name");

        // Assert
        result.RecipientUserIds.Should().BeEquivalentTo(new[] { agentId });
    }

    [Fact]
    public async Task AddCommentAsync_TargetedRecipient_Employee_Denied()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();

        var ticket = BuildTicket(ticketId, "Open", creatorId);
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());
        _userLookupMock
            .Setup(l => l.GetRolesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [employeeId] = "Employee" });

        // Act - targeting a plain employee (not the creator) is rejected
        var act = () => _sut.AddCommentAsync(
            ticketId,
            new AddCommentRequest("To employee", false, RecipientUserIds: new[] { employeeId }),
            adminId,
            "Admin",
            "Admin Name");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*agents, managers, or the ticket creator*");
    }

    [Fact]
    public async Task AddCommentAsync_TargetedRecipient_Admin_Denied()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var adminAuthorId = Guid.NewGuid();
        var adminRecipientId = Guid.NewGuid();

        var ticket = BuildTicket(ticketId, "Open", creatorId);
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());
        _userLookupMock
            .Setup(l => l.GetRolesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [adminRecipientId] = "Admin" });

        // Act - admins see everything already, so targeting one is rejected
        var act = () => _sut.AddCommentAsync(
            ticketId,
            new AddCommentRequest("To admin", false, RecipientUserIds: new[] { adminRecipientId }),
            adminAuthorId,
            "Admin",
            "Admin Name");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*agents, managers, or the ticket creator*");
    }

    [Fact]
    public async Task AddCommentAsync_ReplyToTargeted_NewEmployeeRecipient_Denied()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var inheritedId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();

        var ticket = BuildTicket(ticketId, "Open", creatorId);

        var parentComment = new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorUserId = Guid.NewGuid(),
            Content = "Targeted parent",
            IsPrivate = true,
            Recipients =
            {
                new TicketCommentRecipient { CommentId = ticketId, RecipientUserId = inheritedId }
            }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());
        _commentRepoMock.Setup(r => r.GetByIdAsync(parentComment.Id)).ReturnsAsync(parentComment);
        _userLookupMock
            .Setup(l => l.GetRolesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [employeeId] = "Employee" });

        // Act - reply keeps the inherited recipient but adds someone not on the parent
        var act = () => _sut.AddCommentAsync(
            ticketId,
            new AddCommentRequest("Reply", false, ParentCommentId: parentComment.Id,
                RecipientUserIds: new[] { inheritedId, employeeId }),
            adminId,
            "Admin",
            "Admin Name");

        // Assert - the new recipient is rejected by the subset rule; role lookup is never hit
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*subset of the parent comment's recipients*");
        _userLookupMock.Verify(
            l => l.GetRolesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task AddCommentAsync_ReplyToTargeted_NewAgentRecipient_Denied()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var inheritedId = Guid.NewGuid();
        var newAgentId = Guid.NewGuid();

        var ticket = BuildTicket(ticketId, "Open", creatorId);

        var parentComment = new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorUserId = Guid.NewGuid(),
            Content = "Targeted parent",
            IsPrivate = true,
            Recipients =
            {
                new TicketCommentRecipient { CommentId = ticketId, RecipientUserId = inheritedId }
            }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());
        _commentRepoMock.Setup(r => r.GetByIdAsync(parentComment.Id)).ReturnsAsync(parentComment);
        _userLookupMock
            .Setup(l => l.GetRolesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [newAgentId] = "IT Support Agent" });

        // Act - reply keeps the inherited recipient but adds a legit agent not on the parent
        var act = () => _sut.AddCommentAsync(
            ticketId,
            new AddCommentRequest("Reply", false, ParentCommentId: parentComment.Id,
                RecipientUserIds: new[] { inheritedId, newAgentId }),
            adminId,
            "Admin",
            "Admin Name");

        // Assert - even valid agents can't be added to a reply; only the parent's set may be used
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*subset of the parent comment's recipients*");
        _userLookupMock.Verify(
            l => l.GetRolesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task AddCommentAsync_ReplyToTargeted_FullParentSet_Allowed()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var recipientA = Guid.NewGuid();
        var recipientB = Guid.NewGuid();

        var ticket = BuildTicket(ticketId, "Open", creatorId);

        var parentComment = new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorUserId = Guid.NewGuid(),
            Content = "Targeted parent",
            IsPrivate = true,
            Recipients =
            {
                new TicketCommentRecipient { CommentId = ticketId, RecipientUserId = recipientA },
                new TicketCommentRecipient { CommentId = ticketId, RecipientUserId = recipientB }
            }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());
        _commentRepoMock.Setup(r => r.GetByIdAsync(parentComment.Id)).ReturnsAsync(parentComment);
        _commentRepoMock.Setup(r => r.AddAsync(It.IsAny<TicketComment>())).Returns(Task.CompletedTask);

        // Act - reply keeps exactly the parent's recipients
        var result = await _sut.AddCommentAsync(
            ticketId,
            new AddCommentRequest("Reply", false, ParentCommentId: parentComment.Id,
                RecipientUserIds: new[] { recipientA, recipientB }),
            adminId,
            "Admin",
            "Admin Name");

        // Assert
        result.RecipientUserIds.Should().BeEquivalentTo(new[] { recipientA, recipientB });
    }

    [Fact]
    public async Task AddCommentAsync_ReplyOmittedRecipients_InheritsWithoutValidation()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var inheritedId = Guid.NewGuid();

        var ticket = BuildTicket(ticketId, "Open", creatorId);

        var parentComment = new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorUserId = Guid.NewGuid(),
            Content = "Targeted parent",
            IsPrivate = true,
            Recipients =
            {
                new TicketCommentRecipient { CommentId = ticketId, RecipientUserId = inheritedId }
            }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());
        _commentRepoMock.Setup(r => r.GetByIdAsync(parentComment.Id)).ReturnsAsync(parentComment);

        // Act - recipients omitted entirely; the default lookup (no roles) must not be consulted
        var result = await _sut.AddCommentAsync(
            ticketId,
            new AddCommentRequest("Reply", false, ParentCommentId: parentComment.Id),
            adminId,
            "Admin",
            "Admin Name");

        // Assert
        result.RecipientUserIds.Should().BeEquivalentTo(new[] { inheritedId });
        _userLookupMock.Verify(l => l.GetRolesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AddCommentAsync_ReplyToPrivateComment_InheritsPrivateAudience()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var ticketCreatorUserId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var parentAuthorId = Guid.NewGuid();

        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = ticketCreatorUserId,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        var parentComment = new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorUserId = parentAuthorId,
            Content = "Private parent",
            IsPrivate = true
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, AgentUserId = agentId, AssignedAt = DateTime.UtcNow }
        });
        _commentRepoMock.Setup(r => r.GetByIdAsync(parentComment.Id)).ReturnsAsync(parentComment);

        // Act - creator replies to a private comment
        var result = await _sut.AddCommentAsync(
            ticketId,
            new AddCommentRequest("Private reply", false, ParentCommentId: parentComment.Id),
            ticketCreatorUserId,
            "Employee",
            "Creator Name");

        // Assert - reply inherits the private audience (assigned agent + parent author; creator is the author)
        result.IsPrivate.Should().BeTrue();
        result.RecipientUserIds.Should().BeEquivalentTo(new[] { agentId });

        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.commented" &&
            JsonSerializer.Deserialize<TicketCommentedEvent>(m.Payload, (JsonSerializerOptions?)null)!.RecipientUserIds!.OrderBy(x => x)
                .SequenceEqual(new[] { agentId, parentAuthorId }.OrderBy(x => x))
        )), Times.Once);
    }

    [Fact]
    public async Task AddCommentAsync_ReplyToTargetedComment_ExplicitFewerRecipients_IsRespected()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var ticketCreatorUserId = Guid.NewGuid();
        var parentRecipient1 = Guid.NewGuid();
        var parentRecipient2 = Guid.NewGuid();

        var ticket = BuildTicket(ticketId, "Open", ticketCreatorUserId);

        var parentComment = new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorUserId = Guid.NewGuid(),
            Content = "Targeted parent",
            IsPrivate = true,
            Recipients =
            {
                new TicketCommentRecipient { CommentId = ticketId, RecipientUserId = parentRecipient1 },
                new TicketCommentRecipient { CommentId = ticketId, RecipientUserId = parentRecipient2 }
            }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());
        _commentRepoMock.Setup(r => r.GetByIdAsync(parentComment.Id)).ReturnsAsync(parentComment);

        // Act - creator replies but explicitly selects only one of the two parent recipients
        var result = await _sut.AddCommentAsync(
            ticketId,
            new AddCommentRequest("Reply to fewer", false, ParentCommentId: parentComment.Id, RecipientUserIds: new[] { parentRecipient1 }),
            ticketCreatorUserId,
            "Employee",
            "Creator Name");

        // Assert - the explicit list is respected instead of inheriting all parent recipients
        result.IsPrivate.Should().BeTrue();
        result.RecipientUserIds.Should().BeEquivalentTo(new[] { parentRecipient1 });
    }

    [Fact]
    public async Task AddCommentAsync_ReplyToTargetedComment_ExplicitEmptyRecipients_BecomesPublic()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var ticketCreatorUserId = Guid.NewGuid();
        var parentRecipient1 = Guid.NewGuid();
        var parentRecipient2 = Guid.NewGuid();

        var ticket = BuildTicket(ticketId, "Open", ticketCreatorUserId);

        var parentComment = new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorUserId = Guid.NewGuid(),
            Content = "Targeted parent",
            IsPrivate = true,
            Recipients =
            {
                new TicketCommentRecipient { CommentId = ticketId, RecipientUserId = parentRecipient1 },
                new TicketCommentRecipient { CommentId = ticketId, RecipientUserId = parentRecipient2 }
            }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());
        _commentRepoMock.Setup(r => r.GetByIdAsync(parentComment.Id)).ReturnsAsync(parentComment);

        // Act - creator replies with all recipients deselected
        var result = await _sut.AddCommentAsync(
            ticketId,
            new AddCommentRequest("Reply with no recipients", false, ParentCommentId: parentComment.Id, RecipientUserIds: Array.Empty<Guid>()),
            ticketCreatorUserId,
            "Employee",
            "Creator Name");

        // Assert - explicit empty list means no inherited recipients and the reply is public
        result.IsPrivate.Should().BeFalse();
        result.RecipientUserIds.Should().BeEmpty();
    }

    [Fact]
    public async Task AddCommentAsync_PlainComment_NotifiesCreatorAndAssignedAgents()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var ticketCreatorUserId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var authorUserId = Guid.NewGuid();

        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = ticketCreatorUserId,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, AgentUserId = agentId, AssignedAt = DateTime.UtcNow }
        });

        // Act - unrelated employee posts a public comment
        var result = await _sut.AddCommentAsync(
            ticketId,
            new AddCommentRequest("Hello", false),
            authorUserId,
            "Employee",
            "Author Name");

        // Assert - public comment, creator + assigned agent notified, author excluded
        result.IsPrivate.Should().BeFalse();
        result.RecipientUserIds.Should().BeEmpty();

        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.commented" &&
            JsonSerializer.Deserialize<TicketCommentedEvent>(m.Payload, (JsonSerializerOptions?)null)!.RecipientUserIds!.OrderBy(x => x)
                .SequenceEqual(new[] { ticketCreatorUserId, agentId }.OrderBy(x => x))
        )), Times.Once);
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
        var viewerUserId = Guid.NewGuid();
        var ticketCreatorUserId = Guid.NewGuid();
        var comments = new List<TicketComment>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, AuthorUserId = Guid.NewGuid(), Content = "Public comment", IsPrivate = false, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), TicketId = ticketId, AuthorUserId = viewerUserId, Content = "Private note", IsPrivate = true, CreatedAt = DateTime.UtcNow }
        };

        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = ticketCreatorUserId,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());
        _commentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId, viewerUserId, "Employee", ticketCreatorUserId, It.IsAny<HashSet<Guid>>())).ReturnsAsync(comments);

        // Act
        var result = await _sut.GetCommentsAsync(ticketId, viewerUserId, "Employee");

        // Assert
        result.Should().HaveCount(2);
        result[0].Content.Should().Be("Public comment");
        result[0].IsPrivate.Should().BeFalse();
        result[1].Content.Should().Be("Private note");
        result[1].IsPrivate.Should().BeTrue();
    }

    [Fact]
    public async Task GetCommentsAsync_PrivateComment_OnlyVisibleToPermittedRoles()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var viewerUserId = Guid.NewGuid();
        var ticketCreatorUserId = Guid.NewGuid();
        var publicComments = new List<TicketComment>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, AuthorUserId = Guid.NewGuid(), Content = "Public comment", IsPrivate = false, CreatedAt = DateTime.UtcNow }
        };

        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = ticketCreatorUserId,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());
        _commentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId, viewerUserId, "Employee", ticketCreatorUserId, It.IsAny<HashSet<Guid>>())).ReturnsAsync(publicComments);

        // Act
        var result = await _sut.GetCommentsAsync(ticketId, viewerUserId, "Employee");

        // Assert
        result.Should().HaveCount(1);
        result[0].IsPrivate.Should().BeFalse();
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

    [Fact]
    public async Task ClaimTicketAsync_Success_AssignsAndChangesStatusToInProgress()
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
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });
        _statusRepoMock.Setup(r => r.GetByNameAsync("In Progress")).ReturnsAsync(new Status { Id = 2, Name = "In Progress" });
        _assignmentRepoMock.Setup(r => r.GetActiveAssignmentAsync(ticketId, userId))
            .ReturnsAsync((TicketAssignment?)null);

        // Act
        var result = await _sut.ClaimTicketAsync(ticketId, userId, "Test User");

        // Assert
        result.AgentUserId.Should().Be(userId);
        result.AssignedByUserId.Should().Be(userId);
        result.UnassignedAt.Should().BeNull();

        ticket.StatusId.Should().Be(2);

        _assignmentRepoMock.Verify(r => r.AddAsync(It.Is<TicketAssignment>(a =>
            a.TicketId == ticketId &&
            a.AgentUserId == userId &&
            a.AssignedByUserId == userId
        )), Times.Once);

        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.assigned"
        )), Times.Once);

        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(m =>
            m.EventType == "ticket.status_changed"
        )), Times.Once);

        _auditLogRepoMock.Verify(r => r.AddAsync(It.Is<TicketAuditLogEntry>(e =>
            e.FieldChanged == "Assignment"
        )), Times.Once);

        _auditLogRepoMock.Verify(r => r.AddAsync(It.Is<TicketAuditLogEntry>(e =>
            e.FieldChanged == "Status" &&
            e.OldValue == "Open" &&
            e.NewValue == "In Progress"
        )), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClaimTicketAsync_TicketNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync((Ticket?)null);

        // Act
        var act = () => _sut.ClaimTicketAsync(ticketId, Guid.NewGuid(), "Test User");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Ticket not found*");
    }

    [Fact]
    public async Task ClaimTicketAsync_TicketNotOpen_ThrowsInvalidOperationException()
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
            StatusId = 2,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 2, Name = "In Progress" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });

        // Act
        var act = () => _sut.ClaimTicketAsync(ticketId, Guid.NewGuid(), "Test User");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only open tickets can be claimed*");
    }

    [Fact]
    public async Task ClaimTicketAsync_AlreadyAssigned_ThrowsInvalidOperationException()
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
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });
        _assignmentRepoMock.Setup(r => r.GetActiveAssignmentAsync(ticketId, userId))
            .ReturnsAsync(new TicketAssignment { Id = Guid.NewGuid(), TicketId = ticketId, AgentUserId = userId });

        // Act
        var act = () => _sut.ClaimTicketAsync(ticketId, userId, "Test User");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already assigned*");
    }

    [Fact]
    public async Task GetOpenUnassignedTicketsAsync_ReturnsFilteredResults()
    {
        // Arrange
        var tickets = new List<Ticket>
        {
            new()
            {
                Id = Guid.NewGuid(), ReferenceNumber = "TKT-000001", Title = "Open Unassigned", Description = "Desc",
                CategoryId = 1, PriorityId = 1, StatusId = 1,
                CreatedByUserId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                Category = new Category { Id = 1, Name = "Hardware" },
                Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
                Status = new Status { Id = 1, Name = "Open" }
            }
        };

        _ticketRepoMock.Setup(r => r.GetOpenUnassignedTicketsAsync(1, 10)).ReturnsAsync(tickets);
        _ticketRepoMock.Setup(r => r.GetOpenUnassignedTicketsCountAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.GetOpenUnassignedTicketsAsync(1, 10);

        // Assert
        result.Tickets.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.Tickets[0].StatusName.Should().Be("Open");
    }

    // ---------- DeleteTicketAsync ----------

    [Fact]
    public async Task DeleteTicketAsync_AdminDeletesOpenTicket_CascadesCleanup()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = creatorId,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };
        var comments = new List<TicketComment>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, AuthorUserId = creatorId, Content = "c", IsPrivate = false }
        };
        var attachments = new List<TicketAttachment>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, FileName = "a.png", FileUrl = $"{ticketId}/a.png" }
        };
        var auditLogs = new List<TicketAuditLogEntry>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, FieldChanged = "Created", ChangedByUserId = creatorId }
        };
        var assignments = new List<TicketAssignment>
        {
            new() { Id = Guid.NewGuid(), TicketId = ticketId, AgentUserId = Guid.NewGuid(), AssignedAt = DateTime.UtcNow }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });
        _commentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId, adminId, "Admin", creatorId, It.IsAny<HashSet<Guid>>())).ReturnsAsync(comments);
        _attachmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(attachments);
        _auditLogRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId, 1, 1000)).ReturnsAsync(auditLogs);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(assignments);

        // Act
        await _sut.DeleteTicketAsync(ticketId, adminId, "Admin");

        // Assert
        _commentRepoMock.Verify(r => r.DeleteAsync(comments[0]), Times.Once);
        _attachmentRepoMock.Verify(r => r.DeleteAsync(attachments[0]), Times.Once);
        _auditLogRepoMock.Verify(r => r.DeleteAsync(auditLogs[0]), Times.Once);
        _assignmentRepoMock.Verify(r => r.DeleteAsync(assignments[0]), Times.Once);
        _ticketRepoMock.Verify(r => r.DeleteAsync(ticket), Times.Once);
        _outboxRepoMock.Verify(r => r.AddAsync(It.Is<OutboxMessage>(message =>
            message.EventType == "ticket.deleted" &&
            message.Payload.Contains(ticketId.ToString()))), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteTicketAsync_CreatorDeletesOwnOpenTicket_Succeeds()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = creatorId,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });
        _commentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId, creatorId, "Admin", creatorId, It.IsAny<HashSet<Guid>>())).ReturnsAsync(new List<TicketComment>());
        _attachmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAttachment>());
        _auditLogRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId, 1, 1000)).ReturnsAsync(new List<TicketAuditLogEntry>());
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());

        // Act
        await _sut.DeleteTicketAsync(ticketId, creatorId, "Employee");

        // Assert
        _ticketRepoMock.Verify(r => r.DeleteAsync(ticket), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteTicketAsync_NonOpenStatus_ThrowsInvalidOperationException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 2,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 2, Name = "In Progress" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });

        // Act
        var act = () => _sut.DeleteTicketAsync(ticketId, adminId, "Admin");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only open tickets can be deleted*");
        _ticketRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Ticket>()), Times.Never);
    }

    [Fact]
    public async Task DeleteTicketAsync_NonAdminNonCreator_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "Test",
            CategoryId = 1,
            PriorityId = 1,
            StatusId = 1,
            CreatedByUserId = creatorId,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            Status = new Status { Id = 1, Name = "Open" }
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _statusRepoMock.Setup(r => r.GetByNameAsync("Open")).ReturnsAsync(new Status { Id = 1, Name = "Open" });

        // Act - managers cannot delete tickets
        var act = () => _sut.DeleteTicketAsync(ticketId, managerId, "Manager");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*creator or an admin*");
        _ticketRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Ticket>()), Times.Never);
    }

    [Fact]
    public async Task DeleteTicketAsync_TicketNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync((Ticket?)null);

        // Act
        var act = () => _sut.DeleteTicketAsync(ticketId, Guid.NewGuid(), "Admin");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Ticket not found*");
    }

    // ---------- GetAgentWorkloadAsync ----------

    [Fact]
    public async Task GetAgentWorkloadAsync_ReturnsMappedWorkload()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var openTicket = new AgentWorkloadTicketEntry(Guid.NewGuid(), "TKT-000001", "Open ticket", "Hardware", "Low", "Open", DateTime.UtcNow, DateTime.UtcNow);
        var resolvedTicket = new AgentWorkloadTicketEntry(Guid.NewGuid(), "TKT-000002", "Resolved ticket", "Software", "High", "Closed", DateTime.UtcNow, DateTime.UtcNow);
        var entries = new List<AgentWorkloadEntry>
        {
            new(agentId, 1, 2, new[] { openTicket }, new[] { resolvedTicket })
        };

        _assignmentRepoMock.Setup(r => r.GetAgentWorkloadAsync()).ReturnsAsync(entries);

        // Act
        var result = await _sut.GetAgentWorkloadAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].AgentUserId.Should().Be(agentId);
        result[0].OpenCount.Should().Be(1);
        result[0].ResolvedCount.Should().Be(2);
        result[0].OpenTickets.Should().HaveCount(1);
        result[0].OpenTickets[0].ReferenceNumber.Should().Be("TKT-000001");
        result[0].ResolvedTickets.Should().HaveCount(1);
        result[0].ResolvedTickets[0].ReferenceNumber.Should().Be("TKT-000002");
    }

    // ---------- UploadAttachmentAsync ----------

    [Fact]
    public async Task UploadAttachmentAsync_TicketNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync((Ticket?)null);

        using var stream = new MemoryStream(new byte[10]);

        // Act
        var act = () => _sut.UploadAttachmentAsync(ticketId, stream, "photo.png", Guid.NewGuid(), "Admin");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Ticket not found*");
        _fileStorageMock.Verify(f => f.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadAttachmentAsync_TooLarge_ThrowsInvalidOperationException()
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

        using var stream = new FakeStream(10 * 1024 * 1024 + 1);

        // Act
        var act = () => _sut.UploadAttachmentAsync(ticketId, stream, "photo.png", Guid.NewGuid(), "Admin");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*maximum allowed size*");
        _fileStorageMock.Verify(f => f.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _attachmentRepoMock.Verify(r => r.AddAsync(It.IsAny<TicketAttachment>()), Times.Never);
    }

    [Fact]
    public async Task UploadAttachmentAsync_NonSeekableStream_SizeCapturedAsZero()
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
        _fileStorageMock.Setup(f => f.SaveFileAsync(It.IsAny<Stream>(), "photo.png", ticketId.ToString()))
            .ReturnsAsync($"{ticketId}/stored.png");

        using var stream = new FakeStream(0) { CanSeekValue = false };

        // Act
        var result = await _sut.UploadAttachmentAsync(ticketId, stream, "photo.png", userId, "Admin");

        // Assert
        result.Size.Should().Be(0);
        _attachmentRepoMock.Verify(r => r.AddAsync(It.Is<TicketAttachment>(a => a.Size == 0)), Times.Once);
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".gif")]
    [InlineData(".svg")]
    [InlineData(".webp")]
    [InlineData(".pdf")]
    [InlineData(".doc")]
    [InlineData(".docx")]
    [InlineData(".xls")]
    [InlineData(".xlsx")]
    [InlineData(".csv")]
    [InlineData(".txt")]
    [InlineData(".zip")]
    [InlineData(".json")]
    [InlineData(".xml")]
    [InlineData(".mp4")]
    [InlineData(".mp3")]
    public async Task UploadAttachmentAsync_AllowedExtensions_AllAccepted(string extension)
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
        _fileStorageMock.Setup(f => f.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), ticketId.ToString()))
            .ReturnsAsync($"{ticketId}/stored{extension}");

        using var stream = new MemoryStream(new byte[64]);

        // Act
        var result = await _sut.UploadAttachmentAsync(ticketId, stream, $"file{extension}", Guid.NewGuid(), "Admin");

        // Assert
        result.FileName.Should().Be($"file{extension}");
        _fileStorageMock.Verify(f => f.SaveFileAsync(It.IsAny<Stream>(), $"file{extension}", ticketId.ToString()), Times.Once);
        _attachmentRepoMock.Verify(r => r.AddAsync(It.Is<TicketAttachment>(a => a.FileName == $"file{extension}")), Times.Once);
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".bat")]
    [InlineData(".sh")]
    [InlineData(".msi")]
    [InlineData(".dll")]
    [InlineData(".cmd")]
    [InlineData(".ps1")]
    [InlineData(".scr")]
    [InlineData(".com")]
    [InlineData(".js")]
    [InlineData(".vbs")]
    [InlineData(".html")]
    [InlineData(".htm")]
    [InlineData(".php")]
    [InlineData(".jar")]
    [InlineData(".apk")]
    public async Task UploadAttachmentAsync_HarmfulExtensions_Rejected(string extension)
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

        using var stream = new MemoryStream(new byte[64]);

        // Act
        var act = () => _sut.UploadAttachmentAsync(ticketId, stream, $"malware{extension}", Guid.NewGuid(), "Admin");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not allowed*");
        _fileStorageMock.Verify(f => f.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _attachmentRepoMock.Verify(r => r.AddAsync(It.IsAny<TicketAttachment>()), Times.Never);
    }

    // ---------- GetStatisticsAsync ----------

    [Fact]
    public async Task GetStatisticsAsync_SlaNonCompliance_ReducesCompliancePercentage()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var firstOfMonth = new DateTime(now.Year, now.Month, 1);
        var from = firstOfMonth.AddMonths(-5);
        var ticketId = Guid.NewGuid();
        var created = firstOfMonth.AddDays(1);
        var tickets = new List<Ticket>
        {
            new() { Id = ticketId, CreatedAt = created, StatusId = 4, PriorityId = 1 }
        };
        var transitions = new List<TicketAuditLogEntry>
        {
            // Resolved 60 hours after creation — Low priority SLA is 48 hours
            new() { Id = Guid.NewGuid(), TicketId = ticketId, NewValue = "Closed", ChangedAt = created.AddHours(60) }
        };
        _ticketRepoMock.Setup(r => r.GetForAnalyticsAsync(from, It.IsAny<DateTime>())).ReturnsAsync(tickets);
        _ticketRepoMock.Setup(r => r.GetUnassignedCountAsync(from, It.IsAny<DateTime>())).ReturnsAsync(0);
        _auditLogRepoMock.Setup(r => r.GetResolutionTransitionsAsync(from, It.IsAny<DateTime>())).ReturnsAsync(transitions);

        // Act
        var result = await _sut.GetStatisticsAsync();

        // Assert
        result.Overview.SlaCompliance.Should().Be(0.0);
        result.Overview.AverageResolutionHours.Should().BeApproximately(60.0, 0.1);
    }

    [Fact]
    public async Task GetStatisticsAsync_AverageResolutionHours_ExcludesUnresolvedTickets()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var firstOfMonth = new DateTime(now.Year, now.Month, 1);
        var from = firstOfMonth.AddMonths(-5);
        var resolvedId = Guid.NewGuid();
        var unresolvedId = Guid.NewGuid();
        var created = firstOfMonth.AddDays(1);
        var tickets = new List<Ticket>
        {
            new() { Id = resolvedId, CreatedAt = created, StatusId = 4, PriorityId = 2 },
            new() { Id = unresolvedId, CreatedAt = created, StatusId = 1, PriorityId = 1 }
        };
        var transitions = new List<TicketAuditLogEntry>
        {
            new() { Id = Guid.NewGuid(), TicketId = resolvedId, NewValue = "Closed", ChangedAt = created.AddHours(12) }
        };
        _ticketRepoMock.Setup(r => r.GetForAnalyticsAsync(from, It.IsAny<DateTime>())).ReturnsAsync(tickets);
        _ticketRepoMock.Setup(r => r.GetUnassignedCountAsync(from, It.IsAny<DateTime>())).ReturnsAsync(0);
        _auditLogRepoMock.Setup(r => r.GetResolutionTransitionsAsync(from, It.IsAny<DateTime>())).ReturnsAsync(transitions);

        // Act
        var result = await _sut.GetStatisticsAsync();

        // Assert
        result.Overview.AverageResolutionHours.Should().BeApproximately(12.0, 0.1);
        result.Overview.SlaCompliance.Should().Be(100.0);
        result.Overview.ResolutionRate.Should().Be(50.0);
    }

    // ---------- AddCommentAsync (file attachments) ----------

    [Fact]
    public async Task AddCommentAsync_WithFiles_SavesAttachmentsAndPersistsComment()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var ticket = BuildTicket(ticketId, "Open", creatorId);

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());
        _fileStorageMock.Setup(f => f.SaveFileAsync(It.IsAny<Stream>(), "photo.png", It.IsAny<string>()))
            .ReturnsAsync($"{ticketId}/comments/stored.png");

        using var fileStream = new MemoryStream(new byte[5]);
        var files = new List<CommentFileUpload> { new("photo.png", fileStream, 5) };

        // Act
        var result = await _sut.AddCommentAsync(
            ticketId,
            new AddCommentRequest("Hello with attachment", false),
            authorId,
            "Employee",
            "Author Name",
            files);

        // Assert
        result.Attachments.Should().HaveCount(1);
        result.Attachments![0].FileName.Should().Be("photo.png");
        _fileStorageMock.Verify(f => f.SaveFileAsync(It.IsAny<Stream>(), "photo.png", It.IsAny<string>()), Times.Once);
        _commentRepoMock.Verify(r => r.AddAsync(It.Is<TicketComment>(c =>
            c.Content == "Hello with attachment" &&
            c.Attachments.Count == 1 &&
            c.Attachments.Single().FileName == "photo.png"
        )), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddCommentAsync_WithDisallowedFileExtension_ThrowsInvalidOperationException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var ticket = BuildTicket(ticketId, "Open", creatorId);

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());

        using var fileStream = new MemoryStream(new byte[5]);
        var files = new List<CommentFileUpload> { new("malware.exe", fileStream, 5) };

        // Act
        var act = () => _sut.AddCommentAsync(
            ticketId,
            new AddCommentRequest("Bad attachment", false),
            authorId,
            "Employee",
            "Author Name",
            files);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not allowed*");
        _fileStorageMock.Verify(f => f.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _commentRepoMock.Verify(r => r.AddAsync(It.IsAny<TicketComment>()), Times.Never);
    }

    [Fact]
    public async Task AddCommentAsync_WithOversizedFile_ThrowsInvalidOperationException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var ticket = BuildTicket(ticketId, "Open", creatorId);

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _assignmentRepoMock.Setup(r => r.GetByTicketIdAsync(ticketId)).ReturnsAsync(new List<TicketAssignment>());

        using var fileStream = new MemoryStream(new byte[5]);
        var files = new List<CommentFileUpload> { new("photo.png", fileStream, 10 * 1024 * 1024 + 1) };

        // Act
        var act = () => _sut.AddCommentAsync(
            ticketId,
            new AddCommentRequest("Oversized attachment", false),
            authorId,
            "Employee",
            "Author Name",
            files);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*maximum allowed size*");
        _fileStorageMock.Verify(f => f.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _commentRepoMock.Verify(r => r.AddAsync(It.IsAny<TicketComment>()), Times.Never);
    }

    // ---------- DeleteCommentAttachmentAsync ----------

    [Fact]
    public async Task DeleteCommentAttachmentAsync_AuthorDeletesOwnAttachment_DeletesFileAndRow()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var ticket = BuildTicket(ticketId, "Open", creatorId);
        var comment = new TicketComment
        {
            Id = commentId,
            TicketId = ticketId,
            AuthorUserId = authorId,
            Content = "Comment with attachment",
            IsPrivate = false
        };
        var attachment = new CommentAttachment
        {
            Id = attachmentId,
            CommentId = commentId,
            FileName = "photo.png",
            FileUrl = $"{ticketId}/comments/{commentId}/stored.png",
            UploadedByUserId = authorId
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _commentRepoMock.Setup(r => r.GetByIdAsync(commentId)).ReturnsAsync(comment);
        _commentAttachmentRepoMock.Setup(r => r.GetByIdAsync(attachmentId)).ReturnsAsync(attachment);

        // Act
        await _sut.DeleteCommentAttachmentAsync(ticketId, commentId, attachmentId, authorId, "Employee");

        // Assert
        _fileStorageMock.Verify(f => f.DeleteFileAsync(attachment.FileUrl), Times.Once);
        _commentAttachmentRepoMock.Verify(r => r.DeleteAsync(attachment), Times.Once);
        _auditLogRepoMock.Verify(r => r.AddAsync(It.Is<TicketAuditLogEntry>(e =>
            e.FieldChanged == "Attachment" && e.NewValue == "Comment attachment 'photo.png' deleted")), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommentAttachmentAsync_NoPermission_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var ticket = BuildTicket(ticketId, "Open", creatorId);
        var comment = new TicketComment
        {
            Id = commentId,
            TicketId = ticketId,
            AuthorUserId = Guid.NewGuid(),
            Content = "Comment",
            IsPrivate = false
        };
        var attachment = new CommentAttachment
        {
            Id = attachmentId,
            CommentId = commentId,
            FileName = "photo.png",
            FileUrl = $"{ticketId}/comments/{commentId}/stored.png",
            UploadedByUserId = Guid.NewGuid()
        };

        _ticketRepoMock.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _commentRepoMock.Setup(r => r.GetByIdAsync(commentId)).ReturnsAsync(comment);
        _commentAttachmentRepoMock.Setup(r => r.GetByIdAsync(attachmentId)).ReturnsAsync(attachment);

        // Act - a non-author, non-creator employee tries to delete
        var act = () => _sut.DeleteCommentAttachmentAsync(ticketId, commentId, attachmentId, strangerId, "Employee");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _fileStorageMock.Verify(f => f.DeleteFileAsync(It.IsAny<string>()), Times.Never);
        _commentAttachmentRepoMock.Verify(r => r.DeleteAsync(It.IsAny<CommentAttachment>()), Times.Never);
    }

    private sealed class FakeStream(long length) : Stream
    {
        public bool CanSeekValue { get; init; } = true;

        public override bool CanRead => true;
        public override bool CanSeek => CanSeekValue;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get; set; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count) { }
    }

    [Fact]
    public async Task GetClosedForIndexAsync_MapsClosedTicketsWithClosedAtFromAudit()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var closedAt = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000001",
            Title = "VPN unavailable",
            Description = "The VPN client cannot connect.",
            StatusId = 4,
            Category = new Category { Id = 1, Name = "Network" },
            Priority = new Priority { Id = 2, Name = "High", Level = 3 },
            UpdatedAt = DateTime.UtcNow
        };

        _ticketRepoMock.Setup(r => r.GetClosedTicketsAsync(1, 100)).ReturnsAsync(new List<Ticket> { ticket });
        _ticketRepoMock.Setup(r => r.GetClosedTicketsCountAsync()).ReturnsAsync(1);
        _auditLogRepoMock.Setup(r => r.GetStatusTransitionsAsync(ticketId)).ReturnsAsync(new List<TicketAuditLogEntry>
        {
            new() { NewValue = "In Progress", ChangedAt = DateTime.UtcNow },
            new() { NewValue = "Closed", ChangedAt = closedAt }
        });

        // Act
        var result = await _sut.GetClosedForIndexAsync(1, 100);

        // Assert
        result.TotalCount.Should().Be(1);
        var item = result.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(ticketId);
        item.ReferenceNumber.Should().Be("TKT-000001");
        item.CategoryName.Should().Be("Network");
        item.PriorityName.Should().Be("High");
        item.ClosedAt.Should().Be(closedAt);
    }

    [Fact]
    public async Task GetClosedForIndexAsync_WhenNoClosedAuditTransition_FallsBackToUpdatedAt()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var updatedAt = new DateTime(2026, 8, 2, 9, 0, 0, DateTimeKind.Utc);
        var ticket = new Ticket
        {
            Id = ticketId,
            ReferenceNumber = "TKT-000002",
            Title = "Printer jam",
            Description = "Paper jam.",
            StatusId = 4,
            Category = new Category { Id = 1, Name = "Hardware" },
            Priority = new Priority { Id = 1, Name = "Low", Level = 1 },
            UpdatedAt = updatedAt
        };

        _ticketRepoMock.Setup(r => r.GetClosedTicketsAsync(1, 100)).ReturnsAsync(new List<Ticket> { ticket });
        _ticketRepoMock.Setup(r => r.GetClosedTicketsCountAsync()).ReturnsAsync(1);
        _auditLogRepoMock.Setup(r => r.GetStatusTransitionsAsync(ticketId)).ReturnsAsync(new List<TicketAuditLogEntry>());

        // Act
        var result = await _sut.GetClosedForIndexAsync(1, 100);

        // Assert
        result.Items.Should().ContainSingle().Which.ClosedAt.Should().Be(updatedAt);
    }
}
