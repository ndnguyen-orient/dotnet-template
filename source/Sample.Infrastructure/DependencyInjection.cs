using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sample.Application.Abstractions;
using Sample.Infrastructure.Persistence;
using Sample.Infrastructure.Security;
using Sample.Infrastructure.Widgets;

namespace Sample.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers infrastructure adapters (port implementations) and the EF Core / PostgreSQL
    /// persistence stack. Call from the API composition root.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AppDb")
            ?? throw new InvalidOperationException(
                "Missing connection string 'ConnectionStrings:AppDb'. Set it in appsettings or the environment.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IWidgetRepository, EfWidgetRepository>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        return services;
    }
}
