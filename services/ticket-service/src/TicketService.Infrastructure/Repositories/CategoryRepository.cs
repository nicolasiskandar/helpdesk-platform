using TicketService.Domain.Entities;
using TicketService.Domain.Interfaces;
using TicketService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TicketService.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly TicketDbContext _context;

    public CategoryRepository(TicketDbContext context)
    {
        _context = context;
    }

    public async Task<Category?> GetByIdAsync(int id)
    {
        return await _context.Categories.FindAsync(id);
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync()
    {
        return await _context.Categories.OrderBy(c => c.Id).ToListAsync();
    }
}
