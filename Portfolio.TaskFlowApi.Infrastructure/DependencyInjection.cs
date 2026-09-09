using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.TaskFlowApi.Core.Abstractions;
using Portfolio.TaskFlowApi.Infrastructure.Persistence;
using Portfolio.TaskFlowApi.Infrastructure.Security;

namespace Portfolio.TaskFlowApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TaskFlowDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<ITaskFlowRepository, TaskFlowRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHashService, AspNetPasswordHashService>();
        return services;
    }
}
