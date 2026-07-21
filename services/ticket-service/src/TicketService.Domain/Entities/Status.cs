namespace TicketService.Domain.Entities;

public class Status
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
