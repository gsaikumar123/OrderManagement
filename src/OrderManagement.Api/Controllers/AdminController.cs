using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Data;
using OrderManagement.Api.Dtos;
using OrderManagement.Api.Infrastructure;
using OrderManagement.Api.Models;

namespace OrderManagement.Api.Controllers;

[ApiController, Route("api/admin"), Authorize(Roles = Roles.Admin)]
public class AdminController(AppDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet("registrations")]
    public async Task<List<UserDto>> List([FromQuery] RegistrationStatus? status, CancellationToken ct)
    {
        var q = from u in db.Users
                join ur in db.UserRoles on u.Id equals ur.UserId
                join r in db.Roles on ur.RoleId equals r.Id
                where r.Name == Roles.Employee
                select u;
        if (status is not null) q = q.Where(u => u.Status == status);
        return await q.AsNoTracking().OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id)
            .Select(u => new UserDto(u.Id, u.FullName, u.Email!, u.Status.ToString(), u.CreatedAt)).ToListAsync(ct);
    }

    [HttpPost("registrations/{id}/approve")]
    public Task<IActionResult> Approve(string id) => Decide(id, RegistrationStatus.Approved);

    [HttpPost("registrations/{id}/reject")]
    public Task<IActionResult> Reject(string id) => Decide(id, RegistrationStatus.Rejected);

    private async Task<IActionResult> Decide(string id, RegistrationStatus target)
    {
        var user = await users.FindByIdAsync(id) ?? throw new AppException(404, "User not found.");
        if (user.Status != RegistrationStatus.Pending) throw new AppException(409, $"Registration is already {user.Status}.");
        user.Status = target;
        var res = await users.UpdateAsync(user);
        if (!res.Succeeded) throw new AppException(400, string.Join(" ", res.Errors.Select(e => e.Description)));
        return Ok(new UserDto(user.Id, user.FullName, user.Email!, user.Status.ToString(), user.CreatedAt));
    }

    // Admin-only: creates an already-approved Employee.
    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployee(RegisterRequest req)
    {
        var user = new ApplicationUser
        {
            UserName = req.Email.Trim(), Email = req.Email.Trim(), FullName = req.Name.Trim(),
            Status = RegistrationStatus.Approved
        };
        var res = await users.CreateAsync(user, req.Password);
        if (!res.Succeeded)
        {
            var dup = res.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail");
            throw new AppException(dup ? 409 : 400, string.Join(" ", res.Errors.Select(e => e.Description)));
        }
        await users.AddToRoleAsync(user, Roles.Employee);
        return StatusCode(201, new UserDto(user.Id, user.FullName, user.Email!, user.Status.ToString(), user.CreatedAt));
    }
}
