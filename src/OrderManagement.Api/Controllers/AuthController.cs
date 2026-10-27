using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Dtos;
using OrderManagement.Api.Infrastructure;
using OrderManagement.Api.Models;
using OrderManagement.Api.Services;

namespace OrderManagement.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(UserManager<ApplicationUser> users, TokenService tokens) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        // Role and status are always set server-side.
        var user = new ApplicationUser
        {
            UserName = req.Email.Trim(), Email = req.Email.Trim(), FullName = req.Name.Trim(),
            Status = RegistrationStatus.Pending
        };
        var res = await users.CreateAsync(user, req.Password);
        if (!res.Succeeded)
        {
            var dup = res.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail");
            throw new AppException(dup ? 409 : 400, string.Join(" ", res.Errors.Select(e => e.Description)));
        }
        await users.AddToRoleAsync(user, Roles.Employee);
        return StatusCode(201, new { message = "Registration received. An admin must approve your account before you can log in." });
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        var user = await users.FindByEmailAsync(req.Email.Trim());
        if (user is null || !await users.CheckPasswordAsync(user, req.Password))
            throw new AppException(401, "Invalid email or password.");

        // Credentials are valid: now give a clear message for non-approved accounts. No token is issued.
        if (user.Status == RegistrationStatus.Pending) throw new AppException(403, "Your registration is pending admin approval.");
        if (user.Status == RegistrationStatus.Rejected) throw new AppException(403, "Your registration was rejected.");

        var role = (await users.GetRolesAsync(user)).First();
        var (token, expires) = tokens.Create(user, role);
        return new LoginResponse(token, expires, user.FullName, user.Email!, role);
    }
}
