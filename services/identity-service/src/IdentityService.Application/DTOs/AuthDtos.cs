using System.ComponentModel.DataAnnotations;

namespace IdentityService.Application.DTOs;

public record RegisterRequest(
    [EmailAddress] string Email,
    string Password,
    string FullName
);

public record LoginRequest(
    [EmailAddress] string Email,
    string Password
);

public record RefreshRequest(
    string RefreshToken
);

public record LogoutRequest(
    string RefreshToken
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt
);

public record UserResponse(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

public record UpdateProfileRequest(
    string FullName,
    [EmailAddress] string Email
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

public record ForgotPasswordRequest(
    [EmailAddress] string Email
);

public record ResetPasswordRequest(
    string Token,
    string NewPassword
);

public record ErrorResponse(
    string Message,
    IDictionary<string, string[]>? Errors = null
);
