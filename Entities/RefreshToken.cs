using Microsoft.EntityFrameworkCore;

namespace Tasked.Entities;

[Index(nameof(ExpiresAt))]
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string TokenHash { get; set; } = "";

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
