namespace Tasked.Jwt;

using System.Security.Claims;

public static class UserService
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var userIdClaim = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException("User ID claim not found.");
        return Guid.Parse(userIdClaim.Value);
    }
}