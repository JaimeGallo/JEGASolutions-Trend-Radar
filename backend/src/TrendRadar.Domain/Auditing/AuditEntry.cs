namespace TrendRadar.Domain.Auditing;

/// <summary>
/// Fila del log de auditoría append-only. La base de datos asigna <see cref="Id"/>,
/// <see cref="OccurredAt"/> y los hashes de la cadena; la aplicación no puede fijarlos.
/// </summary>
public sealed class AuditEntry
{
    private AuditEntry()
    {
    }

    public long Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public Guid? UserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string? EntityId { get; private set; }

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    public string? Reason { get; private set; }

    public byte[] ContentHash { get; private set; } = [];

    public byte[] ChainHash { get; private set; } = [];

    public static AuditEntry Create(
        Guid? userId,
        string action,
        string entityType,
        string? entityId,
        string? oldValueJson,
        string? newValueJson,
        string? reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);

        return new AuditEntry
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValue = oldValueJson,
            NewValue = newValueJson,
            Reason = reason,
        };
    }
}
