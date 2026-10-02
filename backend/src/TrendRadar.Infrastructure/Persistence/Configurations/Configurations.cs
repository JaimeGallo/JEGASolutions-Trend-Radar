using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrendRadar.Domain.Auditing;
using TrendRadar.Domain.Auth;
using TrendRadar.Domain.Taxonomy;
using TrendRadar.Domain.Users;

namespace TrendRadar.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).ValueGeneratedNever();
        b.Property(u => u.Email).HasMaxLength(320).IsRequired();
        b.HasIndex(u => u.Email).IsUnique();
        b.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(u => u.PasswordHash).HasMaxLength(100).IsRequired();
        b.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        b.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
    }
}

internal sealed class SignalDomainConfiguration : IEntityTypeConfiguration<SignalDomain>
{
    public void Configure(EntityTypeBuilder<SignalDomain> b)
    {
        b.ToTable("domains");
        b.HasKey(d => d.Id);
        b.Property(d => d.Code).HasMaxLength(40).IsRequired();
        b.HasIndex(d => d.Code).IsUnique();
        b.Property(d => d.Name).HasMaxLength(120).IsRequired();
        b.HasOne<SignalDomain>().WithMany().HasForeignKey(d => d.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> b)
    {
        b.ToTable("audit_log");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).UseIdentityByDefaultColumn();

        // Los asigna el trigger audit_log_before_insert; EF los lee de vuelta tras el INSERT.
        b.Property(a => a.OccurredAt).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        b.Property(a => a.ContentHash).HasDefaultValueSql("'\\x'::bytea").ValueGeneratedOnAdd();
        b.Property(a => a.ChainHash).HasDefaultValueSql("'\\x'::bytea").ValueGeneratedOnAdd();

        b.Property(a => a.Action).HasMaxLength(40).IsRequired();
        b.Property(a => a.EntityType).HasMaxLength(60).IsRequired();
        b.Property(a => a.EntityId).HasMaxLength(100);
        b.Property(a => a.OldValue).HasColumnType("jsonb");
        b.Property(a => a.NewValue).HasColumnType("jsonb");
        b.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).ValueGeneratedNever();
        b.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(t => t.TokenHash).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(t => t.UserId);
    }
}
