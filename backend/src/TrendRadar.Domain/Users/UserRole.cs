namespace TrendRadar.Domain.Users;

/// <summary>Roles de PRODUCT_SPEC §2. Ningún rol puede modificar registros inmutables.</summary>
public enum UserRole
{
    Owner,
    Reviewer,
    Viewer,
}
