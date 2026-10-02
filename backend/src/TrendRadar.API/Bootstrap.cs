using TrendRadar.Application.Auth;

namespace TrendRadar.API;

/// <summary>Crea el propietario inicial (Bootstrap:OwnerEmail, OwnerName, OwnerPassword) si no hay usuarios.</summary>
internal static partial class Bootstrap
{
    public static async Task EnsureOwnerAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var email = configuration["Bootstrap:OwnerEmail"];
        var password = configuration["Bootstrap:OwnerPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
        var name = configuration["Bootstrap:OwnerName"] ?? "Propietario";
        if (await auth.BootstrapOwnerAsync(email, name, password, CancellationToken.None))
        {
            LogOwnerCreated(logger, email);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Propietario inicial creado: {Email}")]
    private static partial void LogOwnerCreated(ILogger logger, string email);
}
