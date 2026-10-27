using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Models;

namespace OrderManagement.Api.Infrastructure;

public class AppException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public static class PrincipalExtensions
{
    public static string UserId(this ClaimsPrincipal p) => p.FindFirstValue("sub") ?? throw new AppException(401, "Invalid token.");
    public static bool IsAdmin(this ClaimsPrincipal p) => p.IsInRole(Roles.Admin);
}

public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> log)
{
    public async Task Invoke(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (AppException ex) { await Write(ctx, ex.Status, ex.Message); }
        catch (DbUpdateException ex)
        {
            log.LogWarning(ex, "Database update conflict");
            await Write(ctx, 409, "The request conflicted with the current data. Please retry.");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unhandled exception");
            await Write(ctx, 500, "An unexpected error occurred.");   // no stack trace leaked
        }
    }

    private static Task Write(HttpContext ctx, int status, string message)
    {
        if (ctx.Response.HasStarted) return Task.CompletedTask;
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(new { message });
    }
}
