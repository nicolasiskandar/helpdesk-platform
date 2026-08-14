using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _context;

    public UserRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email == email);
    }

    public async Task<bool> HasAdminAsync()
    {
        // Only ACTIVE admins count: a deactivated admin must not block the
        // creation of a replacement (admin lockout otherwise).
        return await _context.Users.AnyAsync(u => u.RoleId == 1 && u.IsActive);
    }

    public async Task<int> GetActiveAdminCountAsync()
    {
        return await _context.Users.CountAsync(u => u.RoleId == 1 && u.IsActive);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(string? search, int? roleId, bool? isActive, int page, int pageSize)
    {
        var query = _context.Users
            .Include(u => u.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
        }

        if (roleId.HasValue)
            query = query.Where(u => u.RoleId == roleId.Value);

        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        return await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync(string? search, int? roleId, bool? isActive)
    {
        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
        }

        if (roleId.HasValue)
            query = query.Where(u => u.RoleId == roleId.Value);

        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        return await query.CountAsync();
    }

    public async Task<IReadOnlyList<(Guid Id, string Email)>> GetEmailsByIdsAsync(IReadOnlyList<Guid> ids)
    {
        if (ids.Count == 0) return Array.Empty<(Guid, string)>();

        var emails = await _context.Users
            .Where(u => ids.Contains(u.Id) && u.IsActive)
            .Select(u => new { u.Id, u.Email })
            .ToListAsync();

        return emails.Select(e => (e.Id, e.Email)).ToList();
    }

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
    }

    public Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(User user)
    {
        _context.Users.Remove(user);
        return Task.CompletedTask;
    }
}
