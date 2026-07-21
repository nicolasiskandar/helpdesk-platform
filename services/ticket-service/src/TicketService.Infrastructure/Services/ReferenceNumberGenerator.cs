using Microsoft.EntityFrameworkCore;
using TicketService.Domain.Interfaces;
using TicketService.Infrastructure.Data;

namespace TicketService.Infrastructure.Services;

public class ReferenceNumberGenerator : IReferenceNumberGenerator
{
    private readonly TicketDbContext _context;

    public ReferenceNumberGenerator(TicketDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateAsync()
    {
        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT NEXT VALUE FOR TicketReferenceSequence";
        var result = await command.ExecuteScalarAsync();
        var sequenceValue = Convert.ToInt64(result);
        return $"TKT-{sequenceValue:D6}";
    }
}
