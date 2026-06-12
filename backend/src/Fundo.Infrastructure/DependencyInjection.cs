using Fundo.Application.Abstractions;
using Fundo.Infrastructure.Persistence;
using Fundo.Infrastructure.Persistence.Repositories;
using Fundo.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fundo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("LoansDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'LoansDatabase' is not configured.");

        services.AddDbContext<LoansDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<ILoanRepository, LoanRepository>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<LoansDbContext>());
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
