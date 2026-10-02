using TrendRadar.Domain.Users;

namespace TrendRadar.API;

public sealed record LoginRequest(string Email, string Password);

public sealed record UserDto(Guid Id, string Email, string DisplayName, string Role, bool IsActive)
{
    public static UserDto From(User u) => new(u.Id, u.Email, u.DisplayName, u.Role.ToString(), u.IsActive);
}

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);

public sealed record CreateUserRequest(string Email, string DisplayName, string Password, UserRole Role);

public sealed record DomainDto(Guid Id, string Code, string Name, Guid? ParentId);

public sealed record AuditEntryDto(
    long Id, DateTimeOffset OccurredAt, Guid? UserId, string Action, string EntityType,
    string? EntityId, string? OldValue, string? NewValue, string? Reason, string ChainHash);

public sealed record IntegrityDto(bool IsValid, long CheckedEntries, long? FirstInvalidEntryId, DateTimeOffset VerifiedAt);
