using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

using Tasked.Data;
using Tasked.DTOs;
using Tasked.Entities;
using Tasked.Jwt;
using Tasked.Services;

namespace Tasked.Controllers;

[ApiController]
[Route("api/auth")]

public class AuthController(ApplicationDbContext db, TokenService tokenService) : ControllerBase
{
    private readonly ApplicationDbContext _db = db;
    private readonly TokenService _tokenService = tokenService;
    private readonly PasswordHasher<User> _hasher = new();

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest dto)
    {
        var existingUser = await _db.Users
        .AsNoTracking()
        .FirstOrDefaultAsync(u => u.Username == dto.Username || u.Email == dto.Email);

        if (existingUser != null)
        {
            if (existingUser.Username == dto.Username)
                return Conflict("Username is already taken.");
            if (existingUser.Email == dto.Email)
                return Conflict("Email already in use.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = dto.Username,
            Email = dto.Email,
        };

        user.Password = _hasher.HashPassword(user, dto.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _tokenService.CreateToken(user);

        return Ok(new AuthResponse(token, user.Id, user.Username));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null)
            return Unauthorized("Invalid email or password.");

        var result = _hasher.VerifyHashedPassword(user, user.Password, dto.Password);
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized("Invalid email or password.");

        var token = _tokenService.CreateToken(user);

        return Ok(new AuthResponse(token, user.Id, user.Username));
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //if (userId == null) return Unauthorized();

        var userId = User.GetUserId();

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        return Ok(new
        {
            user.Id,
            user.Username,
            user.Email,
            user.OrgId
        });
    }
}