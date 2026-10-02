using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrendRadar.Domain.Foresight;
using TrendRadar.Domain.Signals;
using TrendRadar.Domain.Users;

namespace TrendRadar.Infrastructure.Persistence.Configurations;

internal sealed class HypothesisConfiguration : IEntityTypeConfiguration<Hypothesis>
{
    public void Configure(EntityTypeBuilder<Hypothesis> b)
    {
        b.ToTable("hypotheses");
        b.HasKey(h => h.Id);
        b.Property(h => h.Id).ValueGeneratedNever();
        b.Property(h => h.Code).HasMaxLength(20).ValueGeneratedOnAdd();
        b.HasIndex(h => h.Code).IsUnique();
        b.Property(h => h.RecordedAt).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        b.Property(h => h.Statement).HasMaxLength(2000).IsRequired();
        b.Property(h => h.Rationale).HasMaxLength(2000);
        b.Property(h => h.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(h => h.SignalId);
        b.HasOne<Signal>().WithMany().HasForeignKey(h => h.SignalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(h => h.RecordedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PredictionConfiguration : IEntityTypeConfiguration<Prediction>
{
    public void Configure(EntityTypeBuilder<Prediction> b)
    {
        b.ToTable("predictions", t =>
        {
            t.HasCheckConstraint("ck_predictions_confidence", "confidence BETWEEN 1 AND 99");
            t.HasCheckConstraint("ck_predictions_base_rate", "base_rate BETWEEN 1 AND 99");
            t.HasCheckConstraint("ck_predictions_specificity", "specificity BETWEEN 1 AND 5");
        });
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();

        // Asignados por los triggers de la migración Foresight.
        b.Property(p => p.Code).HasMaxLength(20).ValueGeneratedOnAdd();
        b.HasIndex(p => p.Code).IsUnique();
        b.Property(p => p.RecordedAt).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        b.Property(p => p.LockedAt).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        b.Property(p => p.Version).HasDefaultValue(0).ValueGeneratedOnAdd();

        b.Property(p => p.Statement).HasMaxLength(2000).IsRequired();
        b.Property(p => p.ResolutionCriteria).HasMaxLength(2000).IsRequired();
        b.Property(p => p.EvidenceSnapshot).HasMaxLength(4000);
        b.Property(p => p.WithdrawalReason).HasMaxLength(2000);
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        // Una versión solo puede tener una sucesora: la historia es una cadena, no un árbol.
        b.HasIndex(p => p.SupersedesId).IsUnique();
        b.HasIndex(p => new { p.Status, p.HorizonDate });
        b.HasIndex(p => p.SignalId);
        b.HasOne<Signal>().WithMany().HasForeignKey(p => p.SignalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Hypothesis>().WithMany().HasForeignKey(p => p.HypothesisId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Prediction>().WithMany().HasForeignKey(p => p.SupersedesId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(p => p.RecordedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PredictionResolutionConfiguration : IEntityTypeConfiguration<PredictionResolution>
{
    public void Configure(EntityTypeBuilder<PredictionResolution> b)
    {
        b.ToTable("prediction_resolutions", t => t.HasCheckConstraint(
            "ck_prediction_resolutions_partial",
            "(outcome = 'Partial' AND partial_credit BETWEEN 0.05 AND 0.95) OR (outcome <> 'Partial' AND partial_credit IS NULL)"));
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.ResolvedAt).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        b.Property(r => r.Outcome).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.PartialCredit).HasPrecision(3, 2);
        b.Property(r => r.Rationale).HasMaxLength(2000).IsRequired();
        b.Property(r => r.EvidenceUrl).HasMaxLength(2000);
        b.HasIndex(r => r.PredictionId);
        b.HasIndex(r => r.SupersedesId).IsUnique();
        b.HasOne<Prediction>().WithMany().HasForeignKey(r => r.PredictionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PredictionResolution>().WithMany().HasForeignKey(r => r.SupersedesId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(r => r.ResolvedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PredictionSnapshotConfiguration : IEntityTypeConfiguration<PredictionSnapshot>
{
    public void Configure(EntityTypeBuilder<PredictionSnapshot> b)
    {
        b.ToTable("prediction_snapshots");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).UseIdentityByDefaultColumn();
        b.Property(s => s.Snapshot).HasColumnType("jsonb").IsRequired();
        b.Property(s => s.CreatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        b.Property(s => s.ContentHash).HasDefaultValueSql("'\\x'::bytea").ValueGeneratedOnAdd();
        b.Property(s => s.ChainHash).HasDefaultValueSql("'\\x'::bytea").ValueGeneratedOnAdd();
        b.HasIndex(s => s.PredictionId);
        b.HasOne<Prediction>().WithMany().HasForeignKey(s => s.PredictionId).OnDelete(DeleteBehavior.Restrict);
    }
}
