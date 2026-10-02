using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using TrendRadar.Domain.Signals;
using TrendRadar.Domain.Taxonomy;
using TrendRadar.Domain.Users;

namespace TrendRadar.Infrastructure.Persistence.Configurations;

internal sealed class SignalConfiguration : IEntityTypeConfiguration<Signal>
{
    public const string SearchVector = "SearchVector";

    public void Configure(EntityTypeBuilder<Signal> b)
    {
        b.ToTable("signals");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();

        // Asignados o calculados por los triggers de la migración Signals.
        b.Property(s => s.SignalCode).HasMaxLength(40).ValueGeneratedOnAdd();
        b.HasIndex(s => s.SignalCode).IsUnique();
        b.Property(s => s.RecordedAt).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        b.Property(s => s.IsRetrospective).HasDefaultValue(false).ValueGeneratedOnAdd();
        b.Property(s => s.CurrentVersion).HasDefaultValue(0).ValueGeneratedOnAddOrUpdate();

        b.Property(s => s.OriginalTitle).HasMaxLength(200).IsRequired();
        b.Property(s => s.OriginalText).IsRequired();
        b.Property(s => s.RecordedTimeZone).HasColumnName("recorded_tz").HasMaxLength(60);
        b.Property(s => s.ClaimedOriginNote).HasMaxLength(200);
        b.Property(s => s.ImportedFrom).HasMaxLength(200);
        b.Property(s => s.SourceReference).HasMaxLength(500);
        b.Property(s => s.GeographicScope).HasMaxLength(120);
        b.Property(s => s.Stage).HasConversion<string>().HasMaxLength(20);
        b.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(s => s.SourceType).HasConversion<string>().HasMaxLength(30);
        b.Property(s => s.ClaimedOriginPrecision).HasConversion<string>().HasMaxLength(20);
        b.Property(s => s.Confidentiality).HasConversion<string>().HasMaxLength(30);
        b.Ignore(s => s.Classification);

        b.Property<NpgsqlTsVector>(SearchVector).HasComputedColumnSql(
            "to_tsvector('simple', f_unaccent(coalesce(signal_code, '') || ' ' || original_title || ' ' || original_text || ' ' || coalesce(original_context, '')))",
            stored: true);
        b.HasIndex(SearchVector).HasMethod("GIN");
        b.HasIndex(s => new { s.DomainId, s.RecordedAt });

        b.HasOne<SignalDomain>().WithMany().HasForeignKey(s => s.DomainId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SignalDomain>().WithMany().HasForeignKey(s => s.SubdomainId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(s => s.RecordedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SignalVersionConfiguration : IEntityTypeConfiguration<SignalVersion>
{
    public void Configure(EntityTypeBuilder<SignalVersion> b)
    {
        b.ToTable("signal_versions");
        b.HasKey(v => v.Id);
        b.Property(v => v.Id).UseIdentityByDefaultColumn();
        b.Property(v => v.Version).HasDefaultValue(0).ValueGeneratedOnAdd();
        b.Property(v => v.CreatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        b.Property(v => v.ContentHash).HasDefaultValueSql("'\\x'::bytea").ValueGeneratedOnAdd();
        b.Property(v => v.ChainHash).HasDefaultValueSql("'\\x'::bytea").ValueGeneratedOnAdd();
        b.Property(v => v.Title).HasMaxLength(200).IsRequired();
        b.Property(v => v.Text).IsRequired();
        b.Property(v => v.ChangeReason).HasMaxLength(1000).IsRequired();
        b.HasIndex(v => new { v.SignalId, v.Version }).IsUnique();
        b.HasOne<Signal>().WithMany().HasForeignKey(v => v.SignalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(v => v.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EvidenceConfiguration : IEntityTypeConfiguration<Evidence>
{
    public void Configure(EntityTypeBuilder<Evidence> b)
    {
        b.ToTable("evidence");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.RecordedAt).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        b.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
        b.Property(e => e.Role).HasConversion<string>().HasMaxLength(20);
        b.Property(e => e.TimestampAuthority).HasConversion<string>().HasMaxLength(30);
        b.Property(e => e.Level).HasConversion<string>().HasMaxLength(2);
        b.Property(e => e.Description).HasMaxLength(2000).IsRequired();
        b.Property(e => e.Url).HasMaxLength(2000);
        b.Property(e => e.GitRepo).HasMaxLength(300);
        b.Property(e => e.GitCommit).HasMaxLength(40);
        b.Property(e => e.FileName).HasMaxLength(200);
        b.Property(e => e.FileSha256).HasMaxLength(64);
        b.Property(e => e.FileMime).HasMaxLength(100);
        b.HasIndex(e => e.SignalId);
        b.HasOne<Signal>().WithMany().HasForeignKey(e => e.SignalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.RecordedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
