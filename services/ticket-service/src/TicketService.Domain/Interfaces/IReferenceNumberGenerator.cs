namespace TicketService.Domain.Interfaces;

public interface IReferenceNumberGenerator
{
    Task<string> GenerateAsync();
}
