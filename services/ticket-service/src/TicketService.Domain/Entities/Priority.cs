namespace TicketService.Domain.Entities;

public class Priority
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
