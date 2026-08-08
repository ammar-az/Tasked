using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

using Tasked.Data;
using Tasked.DTOs;
using Tasked.Entities;
using Tasked.Jwt;
using Tasked.Services;
using System.Security.Cryptography;
using System.Text;

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
            .FirstOrDefaultAsync(u => u.Username == dto.Username);

        if (existingUser != null)
        {
            if (existingUser.Username == dto.Username)
                return Conflict("Username is already taken.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = dto.Username,
        };

        user.Password = _hasher.HashPassword(user, dto.Password);

        _db.Users.Add(user);
        
        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException e)
        {
            return Conflict(e.InnerException?.Message);
        }

        var token = _tokenService.CreateAccessToken(user);

        return await NewSession(user);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest dto)
    {
        await PurgeExpired();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
        if (user == null)
            return Unauthorized("Invalid email or password.");

        var result = _hasher.VerifyHashedPassword(user, user.Password, dto.Password);
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized("Invalid email or password.");

        return await NewSession(user);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        await PurgeExpired();

        if (!Request.Cookies.TryGetValue("tasked_refresh", out var token))
            return Unauthorized();

        var tokenHash = HashToken(token);

        var existingToken = await _db.RefreshTokens
            .Where(t => t.TokenHash == tokenHash)
            .Include(t => t.User)
            .FirstOrDefaultAsync();

        if(existingToken is null)
            return Unauthorized();

        _db.RefreshTokens.Remove(existingToken);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException e)
        {
            return Conflict(e.InnerException?.Message);
        }

        return await NewSession(existingToken.User);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue("tasked_refresh", out var token))
        {
            var tokenHash = HashToken(token);

            await _db.RefreshTokens
                .Where(t => t.TokenHash == tokenHash)
                .Include(t => t.User)
                .ExecuteDeleteAsync();            
        }
        
        Response.Cookies.Delete(
            "tasked_refresh",
            new CookieOptions{ Path = "api/auth" }
        );

        return NoContent();
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.GetUserId();

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => 
                new UserDto()
                {
                    Id = u.Id,
                    Username = u.Username,
                    OrgId = u.OrgId,
                    OrgName = u.Org == null ? null : u.Org.Name,
                }).SingleOrDefaultAsync();

        if (user == null) return NotFound();

        return Ok(user);
    }
    
    private async Task<IActionResult> NewSession(User user)
    {
        var accessToken = _tokenService.CreateAccessToken(user);
        var refreshToken = CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(14),
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException e)
        {
            return Conflict(e.InnerException?.Message);
        }

        var dto = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            OrgId = user.OrgId,
            OrgName = null,
        };

        Response.Cookies.Append(
            "tasked_refresh",
            refreshToken,
            CreateRefreshCookieOptions()
        );

        return Ok(new AuthResponse(accessToken, dto));
    }

    private async Task PurgeExpired()
    {
        await _db.RefreshTokens
            .Where(token => token.ExpiresAt <= DateTime.UtcNow)
            .ExecuteDeleteAsync();
    }

    private static string CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Convert.ToBase64String(bytes)));
        return Convert.ToBase64String(bytes);
    }
    
    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));

        return Convert.ToHexString(bytes);
    }

    private static CookieOptions CreateRefreshCookieOptions()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(14),
            Path = "api/auth"
        };
    }

}
