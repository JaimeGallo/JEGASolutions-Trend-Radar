using Microsoft.Extensions.DependencyInjection;
using TrendRadar.Application.Auditing;
using TrendRadar.Application.Auth;

namespace TrendRadar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuditRecorder>();
        services.AddScoped<AuthService>();
        services.AddScoped<Signals.SignalService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
