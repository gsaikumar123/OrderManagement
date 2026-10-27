using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Models;

namespace OrderManagement.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider sp, IConfiguration cfg)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");

        if (cfg.GetValue<bool>("Database:UseEnsureCreated")) await db.Database.EnsureCreatedAsync(); // tests only
        else if (!cfg.GetValue<bool>("Database:SkipMigrate")) await db.Database.MigrateAsync();

        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in new[] { Roles.Admin, Roles.Employee })
            if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new IdentityRole(r));

        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var email = cfg["Admin:Email"]; var password = cfg["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            logger.LogWarning("Admin:Email / Admin:Password not configured; first Admin not seeded.");
        else if (await users.FindByEmailAsync(email) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                FullName = cfg["Admin:Name"] ?? "System Admin", Status = RegistrationStatus.Approved
            };
            var res = await users.CreateAsync(admin, password);
            if (!res.Succeeded) throw new InvalidOperationException("Admin seed failed: " + string.Join("; ", res.Errors.Select(e => e.Description)));
            await users.AddToRoleAsync(admin, Roles.Admin);
        }

        if (!await db.Products.AnyAsync())
        {
            db.Products.AddRange(
                new Product { Name = "Mechanical Keyboard", Price = 49.99m, Stock = 50 },
                new Product { Name = "Wireless Mouse", Price = 19.99m, Stock = 100 },
                new Product { Name = "27in Monitor", Price = 189.00m, Stock = 20 },
                new Product { Name = "USB-C Cable", Price = 9.50m, Stock = 200 },
                new Product { Name = "Laptop Stand", Price = 34.00m, Stock = 5 });
            await db.SaveChangesAsync();
        }
    }
}
