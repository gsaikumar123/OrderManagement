using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Models;

namespace OrderManagement.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            e.Property(u => u.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(u => u.Status);
        });

        b.Entity<Product>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.Price).HasPrecision(18, 2);
            e.HasIndex(p => p.Name);
            // DB-level backstop: stock can never go negative even if application logic is bypassed.
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Products_Stock_NonNegative", "[Stock] >= 0");
                t.HasCheckConstraint("CK_Products_Price_Positive", "[Price] > 0");
            });
        });

        b.Entity<Order>(e =>
        {
            e.Property(o => o.OrderNumber).HasMaxLength(32).IsRequired();
            e.Property(o => o.Total).HasPrecision(18, 2);
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(o => o.OrderNumber).IsUnique();
            // Employee listing: filter by owner, newest first, stable tiebreak on Id.
            e.HasIndex(o => new { o.UserId, o.CreatedAt, o.Id });
            // Admin listing filtered by status, newest first.
            e.HasIndex(o => new { o.Status, o.CreatedAt });
            e.HasOne(o => o.User).WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OrderItem>(e =>
        {
            e.Property(i => i.UnitPrice).HasPrecision(18, 2);
            e.HasOne(i => i.Order).WithMany(o => o.Items).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(i => new { i.OrderId, i.ProductId }).IsUnique();
            e.HasIndex(i => i.ProductId);
        });
    }
}
