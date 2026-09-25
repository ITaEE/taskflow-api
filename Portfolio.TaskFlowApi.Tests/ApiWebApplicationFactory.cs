using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Portfolio.TaskFlowApi.Infrastructure.Persistence;

namespace Portfolio.TaskFlowApi.Tests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    internal const string TestOnlySigningKey = "TEST_ONLY_TaskFlowApi_signing_key_64_bytes_long_2026_not_for_runtime";
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public ApiWebApplicationFactory()
    {
        _connection.Open();
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        dbContext.TaskItems.RemoveRange(dbContext.TaskItems);
        dbContext.Projects.RemoveRange(dbContext.Projects);
        dbContext.Users.RemoveRange(dbContext.Users);
        await dbContext.SaveChangesAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "Portfolio.TaskFlowApi.Tests",
                ["Jwt:Audience"] = "Portfolio.TaskFlowApi.Tests.Client",
                ["Jwt:SigningKey"] = TestOnlySigningKey,
                ["Jwt:AccessTokenMinutes"] = "30",
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TaskFlowDbContext>>();
            services.RemoveAll<TaskFlowDbContext>();
            services.AddSingleton(_connection);
            services.AddDbContext<TaskFlowDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
