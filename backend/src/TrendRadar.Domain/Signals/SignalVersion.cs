namespace TrendRadar.Domain.Signals;

/// <summary>
/// Versión append-only de una señal. La v1 reproduce el registro original. La base de datos
/// asigna id (consecutivo), número de versión, fecha y hashes de la cadena.
/// </summary>
public sealed class SignalVersion
{
    private SignalVersion()
    {
    }

    public long Id { get; private set; }

    public Guid SignalId { get; private set; }

    public int Version { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Text { get; private set; } = string.Empty;

    public string? Context { get; private set; }

    public int? Confidence { get; private set; }

    public string ChangeReason { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public byte[] ContentHash { get; private set; } = [];

    public byte[] ChainHash { get; private set; } = [];

    public const string OriginalReason = "Registro original";

    public static SignalVersion Original(Signal s) => new()
    {
        SignalId = s.Id,
        Title = s.OriginalTitle,
        Text = s.OriginalText,
        Context = s.OriginalContext,
        Confidence = s.ConfidenceAtCreation,
        ChangeReason = OriginalReason,
        CreatedBy = s.RecordedBy,
    };

    public static SignalVersion Revision(Guid signalId, string title, string text, string? context, int? confidence, string reason, Guid by) => new()
    {
        SignalId = signalId,
        Title = title.Trim(),
        Text = text.Trim(),
        Context = string.IsNullOrWhiteSpace(context) ? null : context.Trim(),
        Confidence = confidence,
        ChangeReason = reason.Trim(),
        CreatedBy = by,
    };
}
