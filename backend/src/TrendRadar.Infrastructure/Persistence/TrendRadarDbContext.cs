using Microsoft.EntityFrameworkCore;
using TrendRadar.Application.Abstractions;
using TrendRadar.Domain.Auditing;
using TrendRadar.Domain.Auth;
using TrendRadar.Domain.Foresight;
using TrendRadar.Domain.Signals;
using TrendRadar.Domain.Taxonomy;
using TrendRadar.Domain.Users;

namespace TrendRadar.Infrastructure.Persistence;

public sealed class TrendRadarDbContext(DbContextOptions<TrendRadarDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();

    public DbSet<SignalDomain> Domains => Set<SignalDomain>();

    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Signal> Signals => Set<Signal>();

    public DbSet<SignalVersion> SignalVersions => Set<SignalVersion>();

    public DbSet<Evidence> Evidence => Set<Evidence>();

    public DbSet<Hypothesis> Hypotheses => Set<Hypothesis>();

    public DbSet<Prediction> Predictions => Set<Prediction>();

    public DbSet<PredictionResolution> PredictionResolutions => Set<PredictionResolution>();

    public DbSet<PredictionSnapshot> PredictionSnapshots => Set<PredictionSnapshot>();

    /// <summary>Envoltorio IMMUTABLE de unaccent definido en la migración Signals (solo para consultas).</summary>
    public static string Unaccent(string input) => throw new NotSupportedException(input);

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken ct) => await SaveChangesAsync(ct);

    async Task IUnitOfWork.InTransactionAsync(Func<Task> work, CancellationToken ct)
    {
        await using var tx = await Database.BeginTransactionAsync(ct);
        await work();
        await tx.CommitAsync(ct);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrendRadarDbContext).Assembly);
        modelBuilder.HasDbFunction(typeof(TrendRadarDbContext).GetMethod(nameof(Unaccent))!).HasName("f_unaccent");
    }
}
