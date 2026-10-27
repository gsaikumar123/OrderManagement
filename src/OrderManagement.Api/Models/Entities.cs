using Microsoft.AspNetCore.Identity;

namespace OrderManagement.Api.Models;

public enum RegistrationStatus { Pending, Approved, Rejected }
public enum OrderStatus { Active, Cancelled }

public static class Roles
{
    public const string Admin = "Admin";
    public const string Employee = "Employee";
}

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = "";
    public RegistrationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }   // decimal(18,2), single currency (USD assumed)
    public int Stock { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }  // snapshot of price at order time
}
