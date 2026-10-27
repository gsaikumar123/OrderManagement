using System.Net;
using System.Net.Http.Json;
using OrderManagement.Api.Dtos;

namespace OrderManagement.Tests;

public class RequiredScenarioTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Pending_login_is_blocked_and_no_token_issued()
    {
        var email = await f.RegisterAsync();
        var res = await f.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = TestHelpers.Pw });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        var text = await res.Content.ReadAsStringAsync();
        Assert.Contains("pending", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejected_login_is_blocked_and_no_token_issued()
    {
        var email = await f.RegisterAsync();
        var admin = await f.AdminAsync();
        (await admin.PostAsync($"/api/admin/registrations/{await admin.UserIdAsync(email)}/reject", null)).EnsureSuccessStatusCode();
        var res = await f.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = TestHelpers.Pw });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.DoesNotContain("token", await res.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Employee_calling_admin_api_is_denied()
    {
        var emp = await f.ApprovedEmployeeAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.GetAsync("/api/admin/registrations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.PostAsJsonAsync("/api/admin/employees",
            new RegisterRequest { Name = "x", Email = "x@test.local", Password = TestHelpers.Pw })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.PostAsJsonAsync("/api/products",
            new ProductRequest { Name = "Nope", Price = 1, Stock = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().GetAsync("/api/orders")).StatusCode);
    }

    [Fact]
    public async Task Employee_cannot_read_or_cancel_another_owners_order()
    {
        var admin = await f.AdminAsync();
        var product = await admin.CreateProductAsync(stock: 10);
        var owner = await f.ApprovedEmployeeAsync();
        var other = await f.ApprovedEmployeeAsync();

        var created = await owner.OrderAsync((product.Id, 2));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var order = (await created.Content.ReadFromJsonAsync<OrderDetailDto>())!;

        // Consistent 404 so the order's existence is not disclosed.
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/orders/{order.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/orders/{order.Id}/cancel", null)).StatusCode);
        var list = (await other.GetFromJsonAsync<PagedResult<OrderSummaryDto>>($"/api/orders?search={order.OrderNumber}"))!;
        Assert.Empty(list.Items);
        Assert.Equal(0, list.TotalCount);

        Assert.Equal(8, await admin.StockAsync(product.Id));              // untouched by the denied cancel
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/orders/{order.Id}")).StatusCode); // admin may read
    }

    [Fact]
    public async Task Insufficient_stock_saves_no_order_and_no_partial_stock_change()
    {
        var admin = await f.AdminAsync();
        var plenty = await admin.CreateProductAsync(stock: 10);
        var scarce = await admin.CreateProductAsync(stock: 2);
        var emp = await f.ApprovedEmployeeAsync();

        var res = await emp.OrderAsync((plenty.Id, 3), (scarce.Id, 5));   // second line fails after first was decremented
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

        Assert.Equal(10, await emp.StockAsync(plenty.Id));                // rolled back
        Assert.Equal(2, await emp.StockAsync(scarce.Id));
        var list = (await emp.GetFromJsonAsync<PagedResult<OrderSummaryDto>>("/api/orders"))!;
        Assert.Equal(0, list.TotalCount);
    }

    [Fact]
    public async Task Repeated_cancellation_restores_stock_once_only()
    {
        var admin = await f.AdminAsync();
        var product = await admin.CreateProductAsync(stock: 5);
        var emp = await f.ApprovedEmployeeAsync();
        var order = (await (await emp.OrderAsync((product.Id, 2))).Content.ReadFromJsonAsync<OrderDetailDto>())!;
        Assert.Equal(3, await emp.StockAsync(product.Id));

        Assert.Equal(HttpStatusCode.OK, (await emp.PostAsync($"/api/orders/{order.Id}/cancel", null)).StatusCode);
        Assert.Equal(5, await emp.StockAsync(product.Id));
        Assert.Equal(HttpStatusCode.Conflict, (await emp.PostAsync($"/api/orders/{order.Id}/cancel", null)).StatusCode);
        Assert.Equal(5, await emp.StockAsync(product.Id));                // not 7
    }

    [Fact]
    public async Task Simultaneous_cancellations_restore_stock_exactly_once()
    {
        var admin = await f.AdminAsync();
        var product = await admin.CreateProductAsync(stock: 5);
        var emp = await f.ApprovedEmployeeAsync();
        var order = (await (await emp.OrderAsync((product.Id, 2))).Content.ReadFromJsonAsync<OrderDetailDto>())!;

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => emp.PostAsync($"/api/orders/{order.Id}/cancel", null))));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(5, await emp.StockAsync(product.Id));
    }

    [Fact]
    public async Task Two_simultaneous_orders_for_last_unit_only_one_succeeds()
    {
        var admin = await f.AdminAsync();
        var product = await admin.CreateProductAsync(stock: 1);
        var a = await f.ApprovedEmployeeAsync();
        var b = await f.ApprovedEmployeeAsync();

        var results = await Task.WhenAll(Task.Run(() => a.OrderAsync((product.Id, 1))), Task.Run(() => b.OrderAsync((product.Id, 1))));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(0, await admin.StockAsync(product.Id));
    }
}
