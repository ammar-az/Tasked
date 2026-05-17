using Microsoft.EntityFrameworkCore;
using Tasked.Models;

namespace Tasked.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {

    }

    public DbSet<TempItem> TempItems => Set<TempItem>();
    
}
