using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TrendRadar.Application.Abstractions;
using TrendRadar.Application.Auth;
using TrendRadar.Application.Signals;
using TrendRadar.Infrastructure.Files;
using TrendRadar.Infrastructure.Persistence;
using TrendRadar.Infrastructure.Persistence.Stores;
using TrendRadar.Infrastructure.Security;

namespace TrendRadar.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Cadena de conexión del rol de aplicación (sin UPDATE/DELETE sobre tablas append-only).</summary>
    public const string AppConnectionName = "TrendRadar";

    /// <summary>Cadena de conexión del propietario del esquema, usada solo para migraciones.</summary>
    public const string MigrationsConnectionName = "TrendRadarMigrations";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(AppConnectionName)
            ?? throw new InvalidOperationException($"Falta ConnectionStrings:{AppConnectionName}.");

        services.AddDbContext<TrendRadarDbContext>(o => o
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TrendRadarDbContext>());
        services.AddScoped<IUserStore, UserStore>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<AuditStore>();
        services.AddScoped<IAuditStore>(sp => sp.GetRequiredService<AuditStore>());
        services.AddScoped<IAuditReader>(sp => sp.GetRequiredService<AuditStore>());
        services.AddScoped<ITaxonomyReader, TaxonomyReader>();
        services.AddScoped<IIntegrityVerifier, IntegrityVerifier>();
        services.AddScoped<ISignalStore, SignalStore>();
        services.AddScoped<IDomainLookup, DomainLookup>();
        services.AddScoped<ISignalQueries, SignalQueries>();
        services.AddSingleton<IEvidenceFileStore, LocalEvidenceFileStore>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .Validate(o => System.Text.Encoding.UTF8.GetByteCount(o.JwtSecret) >= 32,
                "Auth:JwtSecret debe tener al menos 32 bytes.")
            .ValidateOnStart();

        return services;
    }

    public static TokenValidationParameters TokenValidation(AuthOptions o) => new()
    {
        ValidIssuer = o.Issuer,
        ValidAudience = o.Audience,
        IssuerSigningKey = JwtAccessTokenIssuer.SigningKey(o),
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = "sub",
        RoleClaimType = "role",
    };

    /// <summary>Aplica las migraciones con el rol propietario del esquema.</summary>
    public static async Task MigrateAsync(IConfiguration configuration, CancellationToken ct = default)
    {
        var cs = configuration.GetConnectionString(MigrationsConnectionName)
            ?? configuration.GetConnectionString(AppConnectionName)
            ?? throw new InvalidOperationException("No hay cadena de conexión para migraciones.");
        var options = new DbContextOptionsBuilder<TrendRadarDbContext>()
            .UseNpgsql(cs, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new TrendRadarDbContext(options);
        await db.Database.MigrateAsync(ct);
    }
}
