using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Data;
using OrderManagement.Api.Dtos;
using OrderManagement.Api.Infrastructure;
using OrderManagement.Api.Models;

namespace OrderManagement.Api.Controllers;

[ApiController, Route("api/products"), Authorize]
public class ProductsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<ProductDto>> List(CancellationToken ct) =>
        await db.Products.AsNoTracking().OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Select(p => new ProductDto(p.Id, p.Name, p.Price, p.Stock)).ToListAsync(ct);

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(ProductRequest req, CancellationToken ct)
    {
        CheckPrecision(req);
        var p = new Product { Name = req.Name.Trim(), Price = req.Price, Stock = req.Stock };
        db.Products.Add(p);
        await db.SaveChangesAsync(ct);
        return Created($"/api/products/{p.Id}", new ProductDto(p.Id, p.Name, p.Price, p.Stock));
    }

    [HttpPut("{id:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<ProductDto> Update(int id, ProductRequest req, CancellationToken ct)
    {
        CheckPrecision(req);
        var name = req.Name.Trim(); var price = req.Price; var stock = req.Stock;
        // Single atomic UPDATE so we never overwrite a concurrent order's stock change from a stale read.
        var n = await db.Products.Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, name).SetProperty(p => p.Price, price).SetProperty(p => p.Stock, stock), ct);
        if (n == 0) throw new AppException(404, "Product not found.");
        return await db.Products.AsNoTracking().Where(p => p.Id == id).Select(p => new ProductDto(p.Id, p.Name, p.Price, p.Stock)).SingleAsync(ct);
    }

    private static void CheckPrecision(ProductRequest r)
    {
        if (decimal.Round(r.Price, 2) != r.Price) throw new AppException(400, "Price may have at most 2 decimal places.");
    }
}
