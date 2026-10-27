using System.ComponentModel.DataAnnotations;
using OrderManagement.Api.Models;

namespace OrderManagement.Api.Dtos;

// Role / status are intentionally NOT part of the request: client-supplied values are ignored.
public class RegisterRequest
{
    [Required, StringLength(100, MinimumLength = 1)] public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Required, StringLength(100)] public string Password { get; set; } = "";
}

public class LoginRequest
{
    [Required] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

public record LoginResponse(string Token, DateTime ExpiresAt, string Name, string Email, string Role);
public record UserDto(string Id, string Name, string Email, string Status, DateTime CreatedAt);

public class ProductRequest
{
    [Required, StringLength(200, MinimumLength = 1)] public string Name { get; set; } = "";
    [Range(0.01, 1_000_000_000)] public decimal Price { get; set; }
    [Range(0, int.MaxValue)] public int Stock { get; set; }
}
public record ProductDto(int Id, string Name, decimal Price, int Stock);

public class OrderItemRequest
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Range(1, 10000)] public int Quantity { get; set; }
}
public class CreateOrderRequest
{
    [Required, MinLength(1, ErrorMessage = "At least one item is required.")]
    public List<OrderItemRequest> Items { get; set; } = new();
}

public class OrderQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }       // order number
    public OrderStatus? Status { get; set; }
    public string? Employee { get; set; }     // admin only; ignored for employees
}

public record OrderSummaryDto(int Id, string OrderNumber, DateTime CreatedAt, decimal Total, OrderStatus Status, string? EmployeeName);
public record OrderItemDto(int ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);
public record OrderDetailDto(int Id, string OrderNumber, DateTime CreatedAt, DateTime? CancelledAt, decimal Total,
    OrderStatus Status, string? EmployeeName, List<OrderItemDto> Items);
public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
