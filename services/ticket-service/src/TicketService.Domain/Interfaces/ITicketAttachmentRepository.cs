using TicketService.Domain.Entities;

namespace TicketService.Domain.Interfaces;

public interface ITicketAttachmentRepository
{
    Task<IReadOnlyList<TicketAttachment>> GetByTicketIdAsync(Guid ticketId);
    Task AddAsync(TicketAttachment attachment);
    Task DeleteAsync(TicketAttachment attachment);
}
