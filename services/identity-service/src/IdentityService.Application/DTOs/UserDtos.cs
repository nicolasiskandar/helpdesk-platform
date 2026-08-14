namespace IdentityService.Application.DTOs;

public record CreateUserRequest(
    string Email,
    string Password,
    string FullName,
    int RoleId
);

public record UpdateUserRequest(
    string? FullName,
    string? Email,
    int? RoleId,
    bool? IsActive
);

public record UserListResponse(
    IReadOnlyList<UserResponse> Users,
    int TotalCount,
    int Page,
    int PageSize
);

public record UserEmailsRequest(
    IReadOnlyList<Guid> UserIds
);

public record UserEmailResponse(
    Guid Id,
    string Email
);

public record UserEmailsResponse(
    IReadOnlyList<UserEmailResponse> Users
);
