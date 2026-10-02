using System.Text.Json;
using TrendRadar.Application.Abstractions;
using TrendRadar.Domain.Auditing;

namespace TrendRadar.Application.Auditing;

/// <summary>
/// Agrega entradas al log de auditoría dentro de la unidad de trabajo actual,
/// de modo que el cambio y su auditoría se confirman juntos o no se confirman.
/// </summary>
public sealed class AuditRecorder(IAuditStore store)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Record(
        Guid? userId,
        string action,
        string entityType,
        string? entityId,
        object? oldValue = null,
        object? newValue = null,
        string? reason = null)
    {
        store.Add(AuditEntry.Create(
            userId,
            action,
            entityType,
            entityId,
            oldValue is null ? null : JsonSerializer.Serialize(oldValue, JsonOptions),
            newValue is null ? null : JsonSerializer.Serialize(newValue, JsonOptions),
            reason));
    }
}
