using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Data;
using OrderManagement.Api.Dtos;
using OrderManagement.Api.Infrastructure;
using OrderManagement.Api.Models;

namespace OrderManagement.Api.Services;

/// Stock consistency strategy: every stock change is a single conditional SQL UPDATE
/// (UPDATE Products SET Stock = Stock - @q WHERE Id = @id AND Stock >= @q) run inside one transaction.
/// The row lock taken by the UPDATE serialises concurrent buyers; the loser sees 0 rows affected and fails.
/// Cancellation uses the same idea: UPDATE Orders SET Status='Cancelled' WHERE Id=@id AND Status='Active'
/// - only the request that flips the status (rows affected = 1) restores stock, so stock is restored exactly once.
/// Products are always processed in ascending Id order to avoid deadlocks between multi-item orders.
public class OrderService(AppDbContext db)
{
    public async Task<OrderDetailDto> CreateAsync(string userId, CreateOrderRequest req, CancellationToken ct)
    {
        // Consolidate duplicate product entries by summing quantities (documented behaviour).
        var lines = req.Items.GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .OrderBy(x => x.ProductId).ToList();
        if (lines.Any(l => l.Qty > 10000)) throw new AppException(400, "Quantity per product must not exceed 10000.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);   // disposed without commit => rollback

        var order = new Order
        {
            OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            UserId = userId, CreatedAt = DateTime.UtcNow, Status = OrderStatus.Active
        };
        decimal total = 0;

        foreach (var l in lines)
        {
            var pid = l.ProductId; var qty = l.Qty;
            var affected = await db.Products.Where(p => p.Id == pid && p.Stock >= qty)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - qty), ct);
            if (affected == 0)
            {
                var name = await db.Products.Where(p => p.Id == pid).Select(p => p.Name).FirstOrDefaultAsync(ct);
                throw name is null
                    ? new AppException(400, $"Product {pid} does not exist.")
                    : new AppException(409, $"Insufficient stock for '{name}'.");
            }
            // Read the price after the row lock is held, so the stored unit price is consistent with the sale.
            var price = await db.Products.AsNoTracking().Where(p => p.Id == pid).Select(p => p.Price).SingleAsync(ct);
            order.Items.Add(new OrderItem { ProductId = pid, Quantity = qty, UnitPrice = price });
            total += price * qty;
        }

        order.Total = total;
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return (await GetAsync(order.Id, userId, true, ct))!;
    }

    public async Task<OrderDetailDto> CancelAsync(int id, string userId, bool isAdmin, CancellationToken ct)
    {
        // Ownership first: inaccessible orders return the same 404 as missing ones.
        var accessible = await db.Orders.AsNoTracking().AnyAsync(o => o.Id == id && (isAdmin || o.UserId == userId), ct);
        if (!accessible) throw new AppException(404, "Order not found.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        DateTime? now = DateTime.UtcNow;
        var flipped = await db.Orders.Where(o => o.Id == id && o.Status == OrderStatus.Active)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Cancelled).SetProperty(o => o.CancelledAt, now), ct);
        if (flipped == 0) throw new AppException(409, "Order is already cancelled.");

        var items = await db.OrderItems.Where(i => i.OrderId == id)
            .Select(i => new { i.ProductId, i.Quantity }).OrderBy(i => i.ProductId).ToListAsync(ct);
        foreach (var i in items)
        {
            var pid = i.ProductId; var qty = i.Quantity;
            await db.Products.Where(p => p.Id == pid)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + qty), ct);
        }
        await tx.CommitAsync(ct);
        return (await GetAsync(id, userId, isAdmin, ct))!;
    }

    public async Task<OrderDetailDto?> GetAsync(int id, string userId, bool isAdmin, CancellationToken ct)
    {
        var q = db.Orders.AsNoTracking().Where(o => o.Id == id);
        if (!isAdmin) q = q.Where(o => o.UserId == userId);   // ownership applied in the query itself
        return await q.Select(o => new OrderDetailDto(o.Id, o.OrderNumber, o.CreatedAt, o.CancelledAt, o.Total, o.Status,
            isAdmin ? o.User.FullName : null,
            o.Items.OrderBy(i => i.Id).Select(i => new OrderItemDto(i.ProductId, i.Product.Name, i.Quantity, i.UnitPrice, i.UnitPrice * i.Quantity)).ToList()))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PagedResult<OrderSummaryDto>> ListAsync(string userId, bool isAdmin, OrderQuery qry, CancellationToken ct)
    {
        if (qry.Page < 1) throw new AppException(400, "Page must be 1 or greater.");
        if (qry.PageSize < 1 || qry.PageSize > 100) throw new AppException(400, "PageSize must be between 1 and 100.");

        IQueryable<Order> q = db.Orders.AsNoTracking();
        if (!isAdmin) q = q.Where(o => o.UserId == userId);     // ownership BEFORE filter/count/page
        else if (!string.IsNullOrWhiteSpace(qry.Employee))
        {
            var emp = qry.Employee.Trim();
            q = q.Where(o => o.User.FullName.Contains(emp));
        }
        if (!string.IsNullOrWhiteSpace(qry.Search))
        {
            var s = qry.Search.Trim();
            q = q.Where(o => o.OrderNumber.Contains(s));
        }
        if (qry.Status is not null) q = q.Where(o => o.Status == qry.Status);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)   // stable sort
            .Skip((qry.Page - 1) * qry.PageSize).Take(qry.PageSize)
            .Select(o => new OrderSummaryDto(o.Id, o.OrderNumber, o.CreatedAt, o.Total, o.Status, isAdmin ? o.User.FullName : null))
            .ToListAsync(ct);
        return new PagedResult<OrderSummaryDto>(items, qry.Page, qry.PageSize, total, (int)Math.Ceiling(total / (double)qry.PageSize));
    }
}
