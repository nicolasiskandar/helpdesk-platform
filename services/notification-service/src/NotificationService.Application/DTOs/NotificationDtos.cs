namespace NotificationService.Application.DTOs;

public record NotificationResponse(
    Guid Id,
    string Type,
    string Title,
    string Message,
    Guid? TicketId,
    string? TicketReferenceNumber,
    bool IsRead,
    DateTime CreatedAt
);

public record NotificationListResponse(
    IReadOnlyList<NotificationResponse> Notifications,
    int TotalCount,
    int Page,
    int PageSize
);

public record PreferenceResponse(
    bool TicketCreatedInApp,
    bool TicketCreatedEmail,
    bool TicketAssignedInApp,
    bool TicketAssignedEmail,
    bool TicketUnassignedInApp,
    bool TicketUnassignedEmail,
    bool TicketStatusChangedInApp,
    bool TicketStatusChangedEmail,
    bool TicketCommentedInApp,
    bool TicketCommentedEmail
);

public record UpdatePreferenceRequest(
    bool? TicketCreatedInApp,
    bool? TicketCreatedEmail,
    bool? TicketAssignedInApp,
    bool? TicketAssignedEmail,
    bool? TicketUnassignedInApp,
    bool? TicketUnassignedEmail,
    bool? TicketStatusChangedInApp,
    bool? TicketStatusChangedEmail,
    bool? TicketCommentedInApp,
    bool? TicketCommentedEmail
);
