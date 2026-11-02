using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Aura.Api;

public class AppUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public int Credits { get; set; }
    public string Plan { get; set; } = "Free";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PlanExpiresAt { get; set; }
    public DateTime LastRefillAt { get; set; } = DateTime.UtcNow;
}

public class Payment
{
    public long Id { get; set; }
    public string OrderId { get; set; } = "";
    public string? PaymentId { get; set; }
    public string UserId { get; set; } = "";
    public string Plan { get; set; } = "";
    public long AmountPaise { get; set; }
    public string Status { get; set; } = "Created";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
}

public class Generation
{
    public long Id { get; set; }
    public string UserId { get; set; } = "";
    public string ToolKey { get; set; } = "";
    public string Template { get; set; } = "";
    public string Input { get; set; } = "";
    public string Output { get; set; } = "";
    public int CreditsUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Generation> Generations => Set<Generation>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Payment>().HasIndex(x => x.OrderId).IsUnique();
        b.Entity<Generation>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.Property(x => x.ToolKey).HasMaxLength(64);
            e.Property(x => x.Template).HasMaxLength(100);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
