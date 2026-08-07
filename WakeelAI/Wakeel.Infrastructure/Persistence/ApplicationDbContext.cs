using Microsoft.EntityFrameworkCore;
using Wakeel.Application.Interfaces;
using Wakeel.Domain.Entities;

namespace Wakeel.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    private readonly ICurrentTenantService _currentTenantService;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentTenantService currentTenantService)
        : base(options)
    {
        _currentTenantService = currentTenantService ?? throw new System.ArgumentNullException(nameof(currentTenantService));
    }

    public DbSet<Company> Companies { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<EmployeeProfile> EmployeeProfiles { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<Department> Departments { get; set; } = null!;
    public DbSet<LeaveBalance> LeaveBalances { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // This will automatically apply CompanyConfiguration, UserConfiguration, 
        // and EmployeeProfileConfiguration from this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Global tenant isolation filters. Inactive (no-op) when no tenant is resolved yet
        // (e.g. during /auth/login, /auth/register-company, /auth/refresh) — strict once
        // TenantResolutionMiddleware has set a tenant from the JWT for this request.
        // Company and RefreshToken are intentionally NOT filtered: Company is the tenant
        // root itself, and RefreshToken lookups happen by hash before any tenant context exists.
        modelBuilder.Entity<User>().HasQueryFilter(u =>
            !_currentTenantService.HasTenant || u.CompanyId == _currentTenantService.CompanyId);

        modelBuilder.Entity<Department>().HasQueryFilter(d =>
            !_currentTenantService.HasTenant || d.CompanyId == _currentTenantService.CompanyId);

        modelBuilder.Entity<EmployeeProfile>().HasQueryFilter(ep =>
            !_currentTenantService.HasTenant || ep.Department.CompanyId == _currentTenantService.CompanyId);

        modelBuilder.Entity<LeaveBalance>().HasQueryFilter(lb =>
            !_currentTenantService.HasTenant || lb.Employee.Department.CompanyId == _currentTenantService.CompanyId);
    }
}
