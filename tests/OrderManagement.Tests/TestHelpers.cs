using System.Net.Http.Headers;
using System.Net.Http.Json;
using OrderManagement.Api.Dtos;

namespace OrderManagement.Tests;

public static class TestHelpers
{
    public const string Pw = "Password1";

    public static async Task<HttpClient> LoginAsync(this ApiFactory f, string email, string password)
    {
        var c = f.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        res.EnsureSuccessStatusCode();
        var body = (await res.Content.ReadFromJsonAsync<LoginResponse>())!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        return c;
    }

    public static Task<HttpClient> AdminAsync(this ApiFactory f) => f.LoginAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword);

    public static async Task<string> RegisterAsync(this ApiFactory f, string? email = null)
    {
        email ??= $"u{Guid.NewGuid():N}@test.local";
        var res = await f.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest { Name = "Test User", Email = email, Password = Pw });
        res.EnsureSuccessStatusCode();
        return email;
    }

    public static async Task<string> UserIdAsync(this HttpClient admin, string email) =>
        (await admin.GetFromJsonAsync<List<UserDto>>("/api/admin/registrations"))!.First(u => u.Email == email).Id;

    /// Registers, approves and logs in a fresh employee.
    public static async Task<HttpClient> ApprovedEmployeeAsync(this ApiFactory f)
    {
        var email = await f.RegisterAsync();
        var admin = await f.AdminAsync();
        (await admin.PostAsync($"/api/admin/registrations/{await admin.UserIdAsync(email)}/approve", null)).EnsureSuccessStatusCode();
        return await f.LoginAsync(email, Pw);
    }

    public static async Task<ProductDto> CreateProductAsync(this HttpClient admin, int stock, decimal price = 10m)
    {
        var res = await admin.PostAsJsonAsync("/api/products", new ProductRequest { Name = "P-" + Guid.NewGuid().ToString("N")[..6], Price = price, Stock = stock });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    public static async Task<int> StockAsync(this HttpClient c, int productId) =>
        (await c.GetFromJsonAsync<List<ProductDto>>("/api/products"))!.First(p => p.Id == productId).Stock;

    public static Task<HttpResponseMessage> OrderAsync(this HttpClient c, params (int productId, int qty)[] items) =>
        c.PostAsJsonAsync("/api/orders", new CreateOrderRequest { Items = items.Select(i => new OrderItemRequest { ProductId = i.productId, Quantity = i.qty }).ToList() });
}
