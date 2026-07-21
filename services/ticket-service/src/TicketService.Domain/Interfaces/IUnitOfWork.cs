namespace TicketService.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    ITicketRepository Tickets { get; }
    ICategoryRepository Categories { get; }
    IPriorityRepository Priorities { get; }
    IStatusRepository Statuses { get; }
    ITicketAssignmentRepository TicketAssignments { get; }
    ITicketCommentRepository TicketComments { get; }
    ITicketAttachmentRepository TicketAttachments { get; }
    ITicketAuditLogRepository TicketAuditLogs { get; }
    IOutboxRepository Outbox { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
