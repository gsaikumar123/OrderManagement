using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Dtos;
using OrderManagement.Api.Infrastructure;
using OrderManagement.Api.Services;

namespace OrderManagement.Api.Controllers;

[ApiController, Route("api/orders"), Authorize]
public class OrdersController(OrderService orders) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<OrderSummaryDto>> List([FromQuery] OrderQuery q, CancellationToken ct) =>
        orders.ListAsync(User.UserId(), User.IsAdmin(), q, ct);

    [HttpGet("{id:int}")]
    public async Task<OrderDetailDto> Get(int id, CancellationToken ct) =>
        await orders.GetAsync(id, User.UserId(), User.IsAdmin(), ct) ?? throw new AppException(404, "Order not found.");

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest req, CancellationToken ct)
    {
        var created = await orders.CreateAsync(User.UserId(), req, ct);   // owner always comes from the token
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<OrderDetailDto> Cancel(int id, CancellationToken ct) =>
        await orders.CancelAsync(id, User.UserId(), User.IsAdmin(), ct);
}
