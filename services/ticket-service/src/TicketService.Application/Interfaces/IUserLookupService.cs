namespace TicketService.Application.Interfaces;

public interface IUserLookupService
{
    Task<IReadOnlyDictionary<Guid, string>> GetRolesByIdsAsync(IEnumerable<Guid> userIds, string accessToken);

    Task<IReadOnlyList<Guid>> GetUserIdsByRoleAsync(string role, string accessToken);
}
