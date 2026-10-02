namespace TrendRadar.Domain.Taxonomy;

/// <summary>Dominio o subdominio de la taxonomía (DATA_MODEL §3.1).</summary>
public sealed class SignalDomain
{
    private SignalDomain()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public Guid? ParentId { get; private set; }

    public bool IsActive { get; private set; }
}
