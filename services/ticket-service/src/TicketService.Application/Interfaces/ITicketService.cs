using TicketService.Application.DTOs;

namespace TicketService.Application.Interfaces;

public interface ITicketService
{
    Task<TicketResponse> CreateTicketAsync(CreateTicketRequest request, Guid createdByUserId);
    Task<TicketResponse> GetTicketByIdAsync(Guid id);
    Task<TicketResponse> GetTicketByReferenceNumberAsync(string referenceNumber);
    Task<TicketListResponse> GetTicketsAsync(int page, int pageSize, DateTime? createdFrom = null, DateTime? createdTo = null);
    Task<TicketListResponse> GetMyTicketsAsync(Guid userId, int page, int pageSize, DateTime? createdFrom = null, DateTime? createdTo = null);
    Task<TicketResponse> UpdateTicketAsync(Guid id, UpdateTicketRequest request, Guid changedByUserId, string requestedByRole);
    Task<TicketResponse> ChangeStatusAsync(Guid id, ChangeStatusRequest request, Guid changedByUserId, string changedByType = "User");
    Task DeleteTicketAsync(Guid id, Guid requestedByUserId, string requestedByRole);

    Task<IReadOnlyList<AssignmentResponse>> GetAssignmentsAsync(Guid ticketId);
    Task<AssignmentResponse> AssignAgentAsync(Guid ticketId, AssignAgentRequest request, Guid assignedByUserId);
    Task UnassignAgentAsync(Guid ticketId, UnassignAgentRequest request, Guid changedByUserId);

    Task<IReadOnlyList<CommentResponse>> GetCommentsAsync(Guid ticketId, bool includeInternal);
    Task<CommentResponse> AddCommentAsync(Guid ticketId, AddCommentRequest request, Guid authorUserId);

    Task<IReadOnlyList<AttachmentResponse>> GetAttachmentsAsync(Guid ticketId);
    Task<AttachmentResponse> AddAttachmentAsync(Guid ticketId, string fileName, string fileUrl, Guid uploadedByUserId);

    Task<AuditLogListResponse> GetAuditLogAsync(Guid ticketId, int page, int pageSize);

    Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync();
    Task<IReadOnlyList<PriorityResponse>> GetPrioritiesAsync();
    Task<IReadOnlyList<StatusResponse>> GetStatusesAsync();
}
