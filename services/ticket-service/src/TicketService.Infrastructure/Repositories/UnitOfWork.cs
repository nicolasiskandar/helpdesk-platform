using TicketService.Domain.Interfaces;
using TicketService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TicketService.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly TicketDbContext _context;

    public UnitOfWork(TicketDbContext context)
    {
        _context = context;
        Tickets = new TicketRepository(context);
        Categories = new CategoryRepository(context);
        Priorities = new PriorityRepository(context);
        Statuses = new StatusRepository(context);
        TicketAssignments = new TicketAssignmentRepository(context);
        TicketComments = new TicketCommentRepository(context);
        TicketAttachments = new TicketAttachmentRepository(context);
        TicketAuditLogs = new TicketAuditLogRepository(context);
        Outbox = new OutboxRepository(context);
    }

    public ITicketRepository Tickets { get; }
    public ICategoryRepository Categories { get; }
    public IPriorityRepository Priorities { get; }
    public IStatusRepository Statuses { get; }
    public ITicketAssignmentRepository TicketAssignments { get; }
    public ITicketCommentRepository TicketComments { get; }
    public ITicketAttachmentRepository TicketAttachments { get; }
    public ITicketAuditLogRepository TicketAuditLogs { get; }
    public IOutboxRepository Outbox { get; }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
