using Microsoft.EntityFrameworkCore;
using Wakeel.Domain.Entities;

namespace Wakeel.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Company> Companies { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<EmployeeProfile> EmployeeProfiles { get; set; } = null!;
<<<<<<< HEAD
=======
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
>>>>>>> a1e16be97fe87f91487bdd174f6d7b6ddcca41f4

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // This will automatically apply CompanyConfiguration, UserConfiguration, 
        // and EmployeeProfileConfiguration from this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
