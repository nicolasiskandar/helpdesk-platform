using System.Text.Json;
using NotificationService.Application.DTOs;
using NotificationService.Application.Events;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace NotificationService.Infrastructure.Services;

public class NotificationBusinessService : INotificationService
{
    private readonly INotificationRepository _notificationRepo;
    private readonly INotificationPreferenceRepository _preferenceRepo;
    private readonly IProcessedMessageRepository _processedMessageRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHubContext<Hub> _hubContext;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<NotificationBusinessService> _logger;

    public NotificationBusinessService(
        INotificationRepository notificationRepo,
        INotificationPreferenceRepository preferenceRepo,
        IProcessedMessageRepository processedMessageRepo,
        IUnitOfWork unitOfWork,
        IHubContext<Hub> hubContext,
        IEmailSender emailSender,
        ILogger<NotificationBusinessService> logger)
    {
        _notificationRepo = notificationRepo;
        _preferenceRepo = preferenceRepo;
        _processedMessageRepo = processedMessageRepo;
        _unitOfWork = unitOfWork;
        _hubContext = hubContext;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetNotificationsAsync(Guid userId, int page, int pageSize, bool? unreadOnly = null)
    {
        var notifications = await _notificationRepo.GetByUserIdAsync(userId, page, pageSize, unreadOnly);
        return notifications.Select(MapToResponse).ToList();
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        return await _notificationRepo.GetCountByUserIdAsync(userId, unreadOnly: true);
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var notification = await _notificationRepo.GetByIdAsync(notificationId);
        if (notification == null || notification.RecipientUserId != userId)
            throw new KeyNotFoundException("Notification not found.");

        await _notificationRepo.MarkAsReadAsync(notificationId);

        var count = await GetUnreadCountAsync(userId);
        await _hubContext.Clients.Group(userId.ToString()).SendAsync("UnreadCountUpdated", count);
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        await _notificationRepo.MarkAllAsReadAsync(userId);

        await _hubContext.Clients.Group(userId.ToString()).SendAsync("UnreadCountUpdated", 0);
    }

    public async Task<PreferenceResponse> GetPreferencesAsync(Guid userId)
    {
        var pref = await _preferenceRepo.GetOrCreateByUserIdAsync(userId);
        return MapPreferenceToResponse(pref);
    }

    public async Task UpdatePreferencesAsync(Guid userId, UpdatePreferenceRequest request)
    {
        var pref = await _preferenceRepo.GetOrCreateByUserIdAsync(userId);

        if (request.TicketCreatedInApp.HasValue) pref.TicketCreatedInApp = request.TicketCreatedInApp.Value;
        if (request.TicketCreatedEmail.HasValue) pref.TicketCreatedEmail = request.TicketCreatedEmail.Value;
        if (request.TicketAssignedInApp.HasValue) pref.TicketAssignedInApp = request.TicketAssignedInApp.Value;
        if (request.TicketAssignedEmail.HasValue) pref.TicketAssignedEmail = request.TicketAssignedEmail.Value;
        if (request.TicketUnassignedInApp.HasValue) pref.TicketUnassignedInApp = request.TicketUnassignedInApp.Value;
        if (request.TicketUnassignedEmail.HasValue) pref.TicketUnassignedEmail = request.TicketUnassignedEmail.Value;
        if (request.TicketStatusChangedInApp.HasValue) pref.TicketStatusChangedInApp = request.TicketStatusChangedInApp.Value;
        if (request.TicketStatusChangedEmail.HasValue) pref.TicketStatusChangedEmail = request.TicketStatusChangedEmail.Value;
        if (request.TicketCommentedInApp.HasValue) pref.TicketCommentedInApp = request.TicketCommentedInApp.Value;
        if (request.TicketCommentedEmail.HasValue) pref.TicketCommentedEmail = request.TicketCommentedEmail.Value;

        await _preferenceRepo.UpdateAsync(pref);
    }

    public async Task ProcessTicketEventAsync(string eventType, string payload)
    {
        try
        {
            switch (eventType)
            {
                case "ticket.created":
                    await HandleTicketCreatedAsync(payload);
                    break;
                case "ticket.closed":
                    await HandleTicketClosedAsync(payload);
                    break;
                case "ticket.assigned":
                    await HandleTicketAssignedAsync(payload);
                    break;
                case "ticket.status_changed":
                    await HandleTicketStatusChangedAsync(payload);
                    break;
                case "ticket.commented":
                    await HandleTicketCommentedAsync(payload);
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize event payload for {EventType}", eventType);
        }
    }

    private async Task HandleTicketCreatedAsync(string payload)
    {
        var evt = JsonSerializer.Deserialize<TicketCreatedEvent>(payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (evt == null) return;

        var recipientIds = (evt.ManagerUserIds ?? Array.Empty<Guid>())
            .Concat(evt.AdminUserIds ?? Array.Empty<Guid>())
            .Distinct();

        foreach (var recipientId in recipientIds)
        {
            if (recipientId == evt.CreatedByUserId) continue;

            await CreateAndDeliverAsync(recipientId, "created",
                $"New ticket: {evt.ReferenceNumber}",
                $"A new ticket has been created: {evt.Title}",
                evt.TicketId, evt.ReferenceNumber);
        }
    }

    private async Task HandleTicketClosedAsync(string payload)
    {
        var evt = JsonSerializer.Deserialize<TicketClosedEvent>(payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (evt == null) return;

        foreach (var adminId in (evt.AdminUserIds ?? Array.Empty<Guid>()).Distinct())
        {
            if (adminId == evt.ClosedByUserId) continue;

            await CreateAndDeliverAsync(adminId, "closed",
                $"Ticket closed: {evt.ReferenceNumber}",
                $"Ticket {evt.ReferenceNumber} was closed on {evt.ClosedAt:dd MMM yyyy} at {evt.ClosedAt:HH:mm} UTC.",
                evt.TicketId, evt.ReferenceNumber);
        }
    }

    private async Task HandleTicketAssignedAsync(string payload)
    {
        var evt = JsonSerializer.Deserialize<TicketAssignedEvent>(payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (evt == null) return;

        await CreateAndDeliverAsync(evt.AgentUserId, "assigned",
            $"Ticket assigned to you: {evt.ReferenceNumber}",
            $"You have been assigned to ticket {evt.ReferenceNumber}.",
            evt.TicketId, evt.ReferenceNumber);
    }

    private async Task HandleTicketStatusChangedAsync(string payload)
    {
        var evt = JsonSerializer.Deserialize<TicketStatusChangedEvent>(payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (evt == null) return;

        foreach (var recipientId in (evt.RecipientUserIds ?? Array.Empty<Guid>()).Distinct())
        {
            if (recipientId == evt.ChangedByUserId) continue;

            await CreateAndDeliverAsync(recipientId, "status_changed",
                $"Ticket status updated: {evt.ReferenceNumber}",
                $"Ticket {evt.ReferenceNumber} status changed from {evt.OldStatus} to {evt.NewStatus}.",
                evt.TicketId, evt.ReferenceNumber);
        }
    }

    private async Task HandleTicketCommentedAsync(string payload)
    {
        var evt = JsonSerializer.Deserialize<TicketCommentedEvent>(payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (evt == null) return;

        var recipientIds = evt.RecipientUserIds ?? Array.Empty<Guid>();

        foreach (var recipientId in recipientIds.Distinct())
        {
            if (recipientId == evt.AuthorUserId) continue;

            await CreateAndDeliverAsync(recipientId, "comment",
                $"{evt.AuthorName} commented on {evt.ReferenceNumber}",
                $"{(evt.IsPrivate ? "Private comment" : "Comment")} on ticket {evt.ReferenceNumber}: \"{evt.Content}\"",
                evt.TicketId, evt.ReferenceNumber, evt.CommentId);
        }
    }

    private async Task CreateAndDeliverAsync(Guid recipientUserId, string type, string title, string message, Guid? ticketId, string? ticketRef, Guid? commentId = null)
    {
        var preference = await _preferenceRepo.GetOrCreateByUserIdAsync(recipientUserId);
        var (inAppEnabled, emailEnabled) = GetPreferenceForType(preference, type);

        if (!inAppEnabled && !emailEnabled) return;

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            Type = type,
            Title = title,
            Message = message,
            TicketId = ticketId,
            TicketReferenceNumber = ticketRef,
            CommentId = commentId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _notificationRepo.AddAsync(notification);

        if (inAppEnabled)
        {
            notification.Deliveries.Add(new NotificationDelivery
            {
                Id = Guid.NewGuid(),
                NotificationId = notification.Id,
                Channel = "in_app",
                Status = "sent",
                SentAt = DateTime.UtcNow
            });

            var response = MapToResponse(notification);
            await _hubContext.Clients.Group(recipientUserId.ToString()).SendAsync("NewNotification", response);
        }

        if (emailEnabled)
        {
            var delivery = new NotificationDelivery
            {
                Id = Guid.NewGuid(),
                NotificationId = notification.Id,
                Channel = "email",
                Status = "pending"
            };
            notification.Deliveries.Add(delivery);

            try
            {
                var emailBody = BuildEmailBody(title, message, ticketRef);
                await _emailSender.SendAsync($"{recipientUserId}@helpdesk.local", title, emailBody);
                delivery.Status = "sent";
                delivery.SentAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email notification to {UserId}", recipientUserId);
                delivery.Status = "failed";
                delivery.Error = ex.Message;
            }
        }

        await _unitOfWork.SaveChangesAsync();

        var unreadCount = await GetUnreadCountAsync(recipientUserId);
        await _hubContext.Clients.Group(recipientUserId.ToString()).SendAsync("UnreadCountUpdated", unreadCount);
    }

    private static (bool inApp, bool email) GetPreferenceForType(NotificationPreference pref, string type) => type switch
    {
        "created" => (pref.TicketCreatedInApp, pref.TicketCreatedEmail),
        "assigned" => (pref.TicketAssignedInApp, pref.TicketAssignedEmail),
        "unassigned" => (pref.TicketUnassignedInApp, pref.TicketUnassignedEmail),
        "status_changed" => (pref.TicketStatusChangedInApp, pref.TicketStatusChangedEmail),
        "closed" => (pref.TicketStatusChangedInApp, pref.TicketStatusChangedEmail),
        "comment" => (pref.TicketCommentedInApp, pref.TicketCommentedEmail),
        _ => (true, true)
    };

    private static string BuildEmailBody(string title, string message, string? ticketRef)
    {
        var ticketSection = !string.IsNullOrEmpty(ticketRef)
            ? $"""
                <tr><td style="padding:0 32px 24px">
                  <a href="http://localhost:3000/tickets?search={Uri.EscapeDataString(ticketRef)}"
                     style="display:inline-block;background:#3b82f6;color:#ffffff;font-weight:600;font-size:14px;
                            text-decoration:none;padding:10px 24px;border-radius:6px;">
                    View {ticketRef}
                  </a>
                </td></tr>
                """
            : "";

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"></head>
            <body style="margin:0;padding:0;background:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f1f5f9;padding:40px 16px">
                <tr><td align="center">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,0.08)">
                    <tr><td style="background:#1a1a2e;padding:24px 32px">
                      <span style="color:#ffffff;font-size:18px;font-weight:700;letter-spacing:-0.01em">IT Service Desk</span>
                    </td></tr>
                    <tr><td style="padding:32px 32px 0">
                      <h2 style="margin:0 0 12px;font-size:20px;font-weight:600;color:#0f172a">{title}</h2>
                      <p style="margin:0;font-size:15px;line-height:1.6;color:#475569">{message}</p>
                    </td></tr>
                    {ticketSection}
                    <tr><td style="padding:0 32px">
                      <hr style="border:none;border-top:1px solid #e2e8f0;margin:0">
                    </td></tr>
                    <tr><td style="padding:20px 32px 24px">
                      <p style="margin:0;font-size:12px;color:#94a3b8">
                        IT Help Desk &middot; Automated notification &middot; Do not reply to this email
                      </p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private static NotificationResponse MapToResponse(Notification n) => new(
        n.Id, n.Type, n.Title, n.Message,
        n.TicketId, n.TicketReferenceNumber,
        n.CommentId, n.IsRead, n.CreatedAt);

    private static PreferenceResponse MapPreferenceToResponse(NotificationPreference p) => new(
        p.TicketCreatedInApp, p.TicketCreatedEmail,
        p.TicketAssignedInApp, p.TicketAssignedEmail,
        p.TicketUnassignedInApp, p.TicketUnassignedEmail,
        p.TicketStatusChangedInApp, p.TicketStatusChangedEmail,
        p.TicketCommentedInApp, p.TicketCommentedEmail);
}
