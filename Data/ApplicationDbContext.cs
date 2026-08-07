using Microsoft.EntityFrameworkCore;
using Tasked.Entities;

namespace Tasked.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Todo>()
            .HasOne(t => t.Assigned)
            .WithMany(u => u.AssignedTodos)
            .HasForeignKey(t => t.AssignedId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Todo>()
            .HasOne(t => t.CreatedBy)
            .WithMany(u => u.CreatedTodos)
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
    
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Todo> Todos => Set<Todo>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
}
