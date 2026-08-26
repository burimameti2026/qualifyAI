using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;
using QualifyAI.Identity.Infrastructure.Identity;

namespace QualifyAI.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContext<ApplicationUser,ApplicationRole,Guid>(options)
{
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict();

        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("Users");
            b.HasIndex(x => new { x.TenantId, x.NormalizedEmail }).IsUnique();
            b.Property(x => x.TenantSlug).HasMaxLength(100).IsRequired();
            b.Property(x => x.FirstName).HasMaxLength(100);
            b.Property(x => x.LastName).HasMaxLength(100);
        });
        builder.Entity<ApplicationRole>(b =>
        {
            b.ToTable("Roles");
            b.HasIndex(x => new { x.TenantId, x.NormalizedName }).IsUnique();
        });
        builder.Entity<UserPermission>(b =>
        {
            b.ToTable("UserPermissions");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.UserId, x.Permission }).IsUnique();
            b.Property(x => x.Permission).HasMaxLength(200).IsRequired();
        });
    }
}
