using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TrendRadar.Infrastructure.Persistence;

/// <summary>Solo para `dotnet ef migrations add`; no se conecta a ninguna base.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TrendRadarDbContext>
{
    public TrendRadarDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<TrendRadarDbContext>()
            .UseNpgsql("Host=localhost;Database=trendradar_design")
            .UseSnakeCaseNamingConvention()
            .Options);
}
