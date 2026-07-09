namespace Tasked.Services;

using System.Security.Claims;

public static class UserService
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var userIdClaim = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException("User ID claim not found.");
        return Guid.Parse(userIdClaim.Value);
    }

    public static string GetUsername(this ClaimsPrincipal user)
    {
        var usernameClaim = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name) ?? throw new UnauthorizedAccessException("Username claim not found.");
        return usernameClaim.Value;
    }

    public static Guid? GetNullableUserId(this ClaimsPrincipal user)
    {
        var userIdClaim = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userIdClaim == null) return null;
        return Guid.Parse(userIdClaim.Value);
    }
}