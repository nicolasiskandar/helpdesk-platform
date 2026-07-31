namespace TicketService.Application.Interfaces;

public interface IUserLookupService
{
    Task<IReadOnlyDictionary<Guid, string>> GetRolesByIdsAsync(IEnumerable<Guid> userIds, string accessToken);
}
