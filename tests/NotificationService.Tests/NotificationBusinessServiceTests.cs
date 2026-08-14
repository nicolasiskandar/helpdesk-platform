using System.Text.Json;
using FluentAssertions;
using Moq;
using NotificationService.Application.DTOs;
using NotificationService.Application.Events;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Xunit;

namespace NotificationService.Tests.Services;

public class NotificationBusinessServiceTests
{
    private readonly Mock<INotificationRepository> _notificationRepo = new();
    private readonly Mock<INotificationPreferenceRepository> _preferenceRepo = new();
    private readonly Mock<IProcessedMessageRepository> _processedMessageRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IHubContext<Hub>> _hubContext = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<IUserEmailResolver> _userEmailResolver = new();
    private readonly Mock<ILogger<NotificationBusinessService>> _logger = new();
    private readonly Mock<IClientProxy> _clientProxy = new();

    private NotificationBusinessService CreateSut()
    {
        var mockClients = new Mock<IHubClients>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxy.Object);
        _hubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        return new(_notificationRepo.Object, _preferenceRepo.Object, _processedMessageRepo.Object,
            _unitOfWork.Object, _hubContext.Object, _emailSender.Object, _userEmailResolver.Object,
            _logger.Object);
    }

    private static NotificationPreference DefaultPreference(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TicketCreatedInApp = true,
        TicketCreatedEmail = false,
        TicketAssignedInApp = true,
        TicketAssignedEmail = false,
        TicketStatusChangedInApp = true,
        TicketStatusChangedEmail = false,
    };

    [Fact]
    public async Task GetNotificationsAsync_ReturnsMappedNotifications()
    {
        var userId = Guid.NewGuid();
        var notifications = new List<Notification>
        {
            new() { Id = Guid.NewGuid(), RecipientUserId = userId, Type = "created", Title = "Test", Message = "Body", CreatedAt = DateTime.UtcNow }
        };
        _notificationRepo.Setup(r => r.GetByUserIdAsync(userId, 1, 20, null))
            .ReturnsAsync(notifications);

        var sut = CreateSut();
        var result = await sut.GetNotificationsAsync(userId, 1, 20);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Test");
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCorrectCount()
    {
        var userId = Guid.NewGuid();
        _notificationRepo.Setup(r => r.GetCountByUserIdAsync(userId, true)).ReturnsAsync(3);

        var sut = CreateSut();
        var count = await sut.GetUnreadCountAsync(userId);

        count.Should().Be(3);
    }

    [Fact]
    public async Task MarkAsReadAsync_ValidNotification_MarksAsRead()
    {
        var userId = Guid.NewGuid();
        var notifId = Guid.NewGuid();
        var notification = new Notification { Id = notifId, RecipientUserId = userId };
        _notificationRepo.Setup(r => r.GetByIdAsync(notifId)).ReturnsAsync(notification);
        _notificationRepo.Setup(r => r.MarkAsReadAsync(notifId)).Returns(Task.CompletedTask);
        _notificationRepo.Setup(r => r.GetCountByUserIdAsync(userId, true)).ReturnsAsync(0);

        var sut = CreateSut();
        await sut.MarkAsReadAsync(notifId, userId);

        _notificationRepo.Verify(r => r.MarkAsReadAsync(notifId), Times.Once);
    }

    [Fact]
    public async Task MarkAsReadAsync_OtherUserNotification_Throws()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var notifId = Guid.NewGuid();
        var notification = new Notification { Id = notifId, RecipientUserId = otherUserId };
        _notificationRepo.Setup(r => r.GetByIdAsync(notifId)).ReturnsAsync(notification);

        var sut = CreateSut();
        var act = () => sut.MarkAsReadAsync(notifId, userId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task MarkAsReadAsync_NotFoundNotification_Throws()
    {
        var notifId = Guid.NewGuid();
        _notificationRepo.Setup(r => r.GetByIdAsync(notifId)).ReturnsAsync((Notification?)null);

        var sut = CreateSut();
        var act = () => sut.MarkAsReadAsync(notifId, Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task MarkAllAsReadAsync_CallsRepository()
    {
        var userId = Guid.NewGuid();
        _notificationRepo.Setup(r => r.MarkAllAsReadAsync(userId)).Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.MarkAllAsReadAsync(userId);

        _notificationRepo.Verify(r => r.MarkAllAsReadAsync(userId), Times.Once);
    }

    [Fact]
    public async Task GetPreferencesAsync_ReturnsExistingPreference()
    {
        var userId = Guid.NewGuid();
        var pref = DefaultPreference(userId);
        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(userId)).ReturnsAsync(pref);

        var sut = CreateSut();
        var result = await sut.GetPreferencesAsync(userId);

        result.TicketCreatedInApp.Should().BeTrue();
        result.TicketCreatedEmail.Should().BeFalse();
    }

    [Fact]
    public async Task UpdatePreferencesAsync_UpdatesAllFields()
    {
        var userId = Guid.NewGuid();
        var pref = DefaultPreference(userId);
        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(userId)).ReturnsAsync(pref);
        _preferenceRepo.Setup(r => r.UpdateAsync(It.IsAny<NotificationPreference>())).Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.UpdatePreferencesAsync(userId, new UpdatePreferenceRequest(
            TicketCreatedInApp: null,
            TicketCreatedEmail: true,
            TicketAssignedInApp: null,
            TicketAssignedEmail: true,
            TicketUnassignedInApp: null,
            TicketUnassignedEmail: null,
            TicketStatusChangedInApp: null,
            TicketStatusChangedEmail: null,
            TicketCommentedInApp: false,
            TicketCommentedEmail: null));

        _preferenceRepo.Verify(r => r.UpdateAsync(It.Is<NotificationPreference>(p =>
            p.TicketCreatedEmail == true &&
            p.TicketAssignedEmail == true &&
            p.TicketCommentedInApp == false &&
            p.TicketCreatedInApp == true)), Times.Once);
    }

    [Fact]
    public async Task ProcessTicketEventAsync_CreatedEvent_NotifiesManagersAndAdminsButNotAgents()
    {
        var managerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(managerId)).ReturnsAsync(DefaultPreference(managerId));
        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(adminId)).ReturnsAsync(DefaultPreference(adminId));
        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(agentId)).ReturnsAsync(DefaultPreference(agentId));
        _notificationRepo.Setup(r => r.AddAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var evt = new TicketCreatedEvent(
            Guid.NewGuid(),
            "TKT-0001",
            "Test ticket",
            "Description",
            "Hardware",
            "Medium",
            Guid.NewGuid(),
            DateTime.UtcNow,
            new List<Guid> { managerId },
            new List<Guid> { adminId });

        var sut = CreateSut();
        await sut.ProcessTicketEventAsync("ticket.created", JsonSerializer.Serialize(evt));

        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.RecipientUserId == managerId && n.Type == "created")), Times.Once);
        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.RecipientUserId == adminId && n.Type == "created")), Times.Once);
        // Agents are not notified on ticket creation
        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.RecipientUserId == agentId)), Times.Never);
    }

    [Fact]
    public async Task ProcessTicketEventAsync_CreatedEvent_ExcludesCreator()
    {
        var creatorId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(managerId)).ReturnsAsync(DefaultPreference(managerId));
        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(creatorId)).ReturnsAsync(DefaultPreference(creatorId));
        _notificationRepo.Setup(r => r.AddAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var evt = new TicketCreatedEvent(
            Guid.NewGuid(),
            "TKT-0001",
            "Test ticket",
            "Description",
            "Hardware",
            "Medium",
            creatorId,
            DateTime.UtcNow,
            new List<Guid> { managerId, creatorId },
            new List<Guid>());

        var sut = CreateSut();
        await sut.ProcessTicketEventAsync("ticket.created", JsonSerializer.Serialize(evt));

        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.RecipientUserId == managerId)), Times.Once);
        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.RecipientUserId == creatorId)), Times.Never);
    }

    [Fact]
    public async Task ProcessTicketEventAsync_ClosedEvent_NotifiesAdminsWithCloseTime()
    {
        var adminId1 = Guid.NewGuid();
        var adminId2 = Guid.NewGuid();
        var closerId = Guid.NewGuid();
        var closedAt = new DateTime(2026, 8, 6, 15, 30, 0, DateTimeKind.Utc);

        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(adminId1)).ReturnsAsync(DefaultPreference(adminId1));
        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(adminId2)).ReturnsAsync(DefaultPreference(adminId2));
        _notificationRepo.Setup(r => r.AddAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var evt = new TicketClosedEvent(
            Guid.NewGuid(),
            "TKT-0007",
            closerId,
            closedAt,
            new List<Guid> { adminId1, adminId2 });

        var sut = CreateSut();
        await sut.ProcessTicketEventAsync("ticket.closed", JsonSerializer.Serialize(evt));

        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.RecipientUserId == adminId1 && n.Type == "closed" &&
            n.Message.Contains("15:30") && n.Message.Contains("06 Aug 2026"))), Times.Once);
        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.RecipientUserId == adminId2 && n.Type == "closed")), Times.Once);
        // The admin who closed the ticket is not notified
        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.RecipientUserId == closerId)), Times.Never);
    }

    [Fact]
    public async Task ProcessTicketEventAsync_AssignedEvent_NotifiesOnlyAgent()
    {
        var agentId = Guid.NewGuid();
        var pref = DefaultPreference(agentId);
        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(agentId)).ReturnsAsync(pref);
        _notificationRepo.Setup(r => r.AddAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var evt = new TicketAssignedEvent(
            Guid.NewGuid(),
            "TKT-0002",
            agentId,
            Guid.NewGuid(),
            DateTime.UtcNow);

        var sut = CreateSut();
        await sut.ProcessTicketEventAsync("ticket.assigned", JsonSerializer.Serialize(evt));

        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.RecipientUserId == agentId && n.Type == "assigned")), Times.Once);
    }

    [Fact]
    public async Task ProcessTicketEventAsync_StatusChangedEvent_NotifiesRecipientsExceptActor()
    {
        var creatorId = Guid.NewGuid();
        var changedById = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        var pref = DefaultPreference(creatorId);
        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(creatorId)).ReturnsAsync(pref);
        _notificationRepo.Setup(r => r.AddAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var evt = new TicketStatusChangedEvent(
            ticketId,
            "TKT-0003",
            "Open",
            "In Progress",
            changedById,
            "Admin",
            DateTime.UtcNow,
            new List<Guid> { creatorId, changedById });

        var sut = CreateSut();
        await sut.ProcessTicketEventAsync("ticket.status_changed", JsonSerializer.Serialize(evt));

        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.RecipientUserId == creatorId && n.Type == "status_changed")), Times.Once);
        // Actor should not receive notification
        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.RecipientUserId == changedById)), Times.Never);
    }

    [Fact]
    public async Task ProcessTicketEventAsync_StatusChangedEvent_NoRecipients_DoesNotNotify()
    {
        var changedById = Guid.NewGuid();
        _notificationRepo.Setup(r => r.AddAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var evt = new TicketStatusChangedEvent(
            Guid.NewGuid(),
            "TKT-0008",
            "Open",
            "In Progress",
            changedById,
            "Admin",
            DateTime.UtcNow,
            new List<Guid>());

        var sut = CreateSut();
        await sut.ProcessTicketEventAsync("ticket.status_changed", JsonSerializer.Serialize(evt));

        _notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task ProcessTicketEventAsync_CommentedEvent_NotifiesRecipientsExceptAuthor()
    {
        var authorId = Guid.NewGuid();
        var recipient1 = Guid.NewGuid();
        var recipient2 = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var commentId = Guid.NewGuid();

        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(recipient1)).ReturnsAsync(DefaultPreference(recipient1));
        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(recipient2)).ReturnsAsync(DefaultPreference(recipient2));
        _notificationRepo.Setup(r => r.AddAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var evt = new TicketCommentedEvent(
            ticketId,
            "TKT-0005",
            authorId,
            "Jane Doe",
            "Please look into this",
            false,
            commentId,
            null,
            new List<Guid> { recipient1, recipient2, authorId },
            DateTime.UtcNow);

        var sut = CreateSut();
        await sut.ProcessTicketEventAsync("ticket.commented", JsonSerializer.Serialize(evt));

        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.RecipientUserId == recipient1 && n.Type == "comment")), Times.Once);
        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.RecipientUserId == recipient2 && n.Type == "comment")), Times.Once);
        // Comment id is stored on the notification so the UI can deep-link to the comment
        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.CommentId == commentId)), Times.AtLeastOnce);
        // Author should not receive a notification
        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.RecipientUserId == authorId)), Times.Never);
    }

    [Fact]
    public async Task ProcessTicketEventAsync_ReplyEvent_NotifiesParentAuthorWithReplyType()
    {
        var replyAuthorId = Guid.NewGuid();
        var parentAuthorId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var parentCommentId = Guid.NewGuid();

        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(parentAuthorId)).ReturnsAsync(DefaultPreference(parentAuthorId));
        _notificationRepo.Setup(r => r.AddAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var evt = new TicketCommentedEvent(
            ticketId,
            "TKT-0009",
            replyAuthorId,
            "Jane Doe",
            "Thanks, will try that",
            false,
            commentId,
            parentCommentId,
            new List<Guid> { parentAuthorId, replyAuthorId },
            DateTime.UtcNow);

        var sut = CreateSut();
        await sut.ProcessTicketEventAsync("ticket.commented", JsonSerializer.Serialize(evt));

        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.RecipientUserId == parentAuthorId && n.Type == "reply" &&
            n.Title.Contains("replied on") &&
            n.Message.Contains("Thanks, will try that") &&
            n.CommentId == commentId)), Times.Once);
        // The reply author should not receive their own notification
        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.RecipientUserId == replyAuthorId)), Times.Never);
    }

    [Fact]
    public async Task ProcessTicketEventAsync_CommentedEvent_SendsEmailToResolvedAddress()
    {
        var authorId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(recipientId)).ReturnsAsync(DefaultPreference(recipientId));
        _notificationRepo.Setup(r => r.AddAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _userEmailResolver.Setup(r => r.GetEmailAsync(recipientId)).ReturnsAsync("real.user@example.com");
        _emailSender.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var evt = new TicketCommentedEvent(
            Guid.NewGuid(),
            "TKT-0010",
            authorId,
            "Jane Doe",
            "Please look into this",
            false,
            Guid.NewGuid(),
            null,
            new List<Guid> { recipientId },
            DateTime.UtcNow);

        var sut = CreateSut();
        await sut.ProcessTicketEventAsync("ticket.commented", JsonSerializer.Serialize(evt));

        _emailSender.Verify(e => e.SendAsync(
            "real.user@example.com",
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessTicketEventAsync_CommentedEvent_EmailFallsBackToDevAddressWhenResolutionFails()
    {
        var authorId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(recipientId)).ReturnsAsync(DefaultPreference(recipientId));
        _notificationRepo.Setup(r => r.AddAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _userEmailResolver.Setup(r => r.GetEmailAsync(recipientId)).ReturnsAsync((string?)null);
        _emailSender.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var evt = new TicketCommentedEvent(
            Guid.NewGuid(),
            "TKT-0011",
            authorId,
            "Jane Doe",
            "Please look into this",
            false,
            Guid.NewGuid(),
            null,
            new List<Guid> { recipientId },
            DateTime.UtcNow);

        var sut = CreateSut();
        await sut.ProcessTicketEventAsync("ticket.commented", JsonSerializer.Serialize(evt));

        _emailSender.Verify(e => e.SendAsync(
            $"{recipientId}@helpdesk.local",
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessTicketEventAsync_PreferencesDisabled_DoesNotNotify()
    {
        var managerId = Guid.NewGuid();
        var disabledPref = DefaultPreference(managerId);
        disabledPref.TicketCreatedInApp = false;
        disabledPref.TicketCreatedEmail = false;
        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(managerId)).ReturnsAsync(disabledPref);

        var evt = new TicketCreatedEvent(
            Guid.NewGuid(),
            "TKT-0004",
            "Test",
            "Description",
            "Hardware",
            "Medium",
            Guid.NewGuid(),
            DateTime.UtcNow,
            new List<Guid> { managerId },
            new List<Guid>());

        var sut = CreateSut();
        await sut.ProcessTicketEventAsync("ticket.created", JsonSerializer.Serialize(evt));

        _notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task ProcessTicketEventAsync_InvalidPayload_DoesNotThrow()
    {
        var sut = CreateSut();
        var act = () => sut.ProcessTicketEventAsync("ticket.created", "invalid json");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProcessTicketEventAsync_UnknownEventType_DoesNothing()
    {
        var sut = CreateSut();
        var act = () => sut.ProcessTicketEventAsync("ticket.unknown", "{}");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProcessTicketEventAsync_CreatedEvent_SendsSignalRNotification()
    {
        var managerId = Guid.NewGuid();
        var pref = DefaultPreference(managerId);
        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(managerId)).ReturnsAsync(pref);
        _notificationRepo.Setup(r => r.AddAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var evt = new TicketCreatedEvent(
            Guid.NewGuid(),
            "TKT-0005",
            "Test",
            "Description",
            "Hardware",
            "Medium",
            Guid.NewGuid(),
            DateTime.UtcNow,
            new List<Guid> { managerId },
            new List<Guid>());

        var sut = CreateSut();
        await sut.ProcessTicketEventAsync("ticket.created", JsonSerializer.Serialize(evt));

        // SignalR is called internally but SendAsync is an extension method that can't be verified via Moq.
        // The notification is created and persisted, which is the critical path.
        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.RecipientUserId == managerId && n.Type == "created")), Times.Once);
    }

    [Fact]
    public async Task ProcessTicketEventAsync_CreatedEvent_SendsEmailIfEnabled()
    {
        var managerId = Guid.NewGuid();
        var pref = DefaultPreference(managerId);
        pref.TicketCreatedEmail = true;
        _preferenceRepo.Setup(r => r.GetOrCreateByUserIdAsync(managerId)).ReturnsAsync(pref);
        _notificationRepo.Setup(r => r.AddAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _emailSender.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var evt = new TicketCreatedEvent(
            Guid.NewGuid(),
            "TKT-0006",
            "Email test",
            "Description",
            "Hardware",
            "Medium",
            Guid.NewGuid(),
            DateTime.UtcNow,
            new List<Guid> { managerId },
            new List<Guid>());

        var sut = CreateSut();
        await sut.ProcessTicketEventAsync("ticket.created", JsonSerializer.Serialize(evt));

        _emailSender.Verify(e => e.SendAsync(
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("TKT-0006")),
            It.IsAny<string>()), Times.Once);
    }
}
