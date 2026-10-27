using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Api.Data;

namespace OrderManagement.Tests;

/// Tests run against a temporary SQLite FILE database (multiple real connections, so parallel requests
/// genuinely contend for the write lock). This is NOT SQL Server: it proves the application logic
/// (conditional UPDATE + transaction) but does not prove SQL Server lock/isolation behaviour.
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Admin#12345";
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"om-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting is applied before Program.cs reads configuration.
        builder.UseSetting("Jwt:Key", "test-signing-key-test-signing-key-123456");
        builder.UseSetting("Jwt:Issuer", "OrderManagement");
        builder.UseSetting("Jwt:Audience", "OrderManagement");
        builder.UseSetting("Admin:Email", AdminEmail);
        builder.UseSetting("Admin:Password", AdminPassword);
        builder.UseSetting("Database:UseEnsureCreated", "true");
        builder.UseSetting("ConnectionStrings:Default", "unused");

        builder.ConfigureServices(services =>
        {
            var d = services.Where(s => s.ServiceType == typeof(DbContextOptions<AppDbContext>)).ToList();
            foreach (var x in d) services.Remove(x);
            services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath};Pooling=False"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { File.Delete(_dbPath); } catch { /* ignore */ }
    }
}
