
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Domain.Users;

namespace PublicServiceCaseManagement.Api.Infrastructure.Persistence;

public sealed class CivicFlowDbContext(
    DbContextOptions<CivicFlowDbContext> options)
    : DbContext(options)
{
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<CaseStatusHistory> CaseStatusHistories =>
        Set<CaseStatusHistory>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<CaseAssignmentHistory> CaseAssignmentHistories =>
        Set<CaseAssignmentHistory>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<ApplicationUser> ApplicationUsers =>
        Set<ApplicationUser>();
    public DbSet<OrganizationMembership> OrganizationMemberships =>
        Set<OrganizationMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Case>(entity =>
        {
            entity.ToTable("cases");

            entity.HasKey(c => c.Id);

            entity.Property(c => c.OrganizationId)
                .IsRequired();

            entity.Property(c => c.ReferenceNumber)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(c => c.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(c => c.Description)
                .HasMaxLength(5000)
                .IsRequired();

            entity.Property(c => c.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(c => c.Priority)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(c => c.AssignedToStaffMemberId);

            entity.Property(c => c.CreatedAt)
                .IsRequired();

            entity.Property(c => c.UpdatedAt)
                .IsRequired();

            entity.HasIndex(c => new
            {
                c.OrganizationId,
                c.AssignedToStaffMemberId
            });

            entity.HasIndex(c => new
            {
                c.OrganizationId,
                c.ReferenceNumber
            }).IsUnique();

            entity.HasIndex(c => new
            {
                c.OrganizationId,
                c.Status
            });

            entity.HasIndex(c => new
            {
                c.OrganizationId,
                c.CreatedAt
            });

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(c => c.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CaseStatusHistory>(entity =>
        {
            entity.ToTable("case_status_history");

            entity.HasKey(h => h.Id);

            entity.Property(h => h.OrganizationId)
                .IsRequired();

            entity.Property(h => h.PreviousStatus)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(h => h.NewStatus)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(h => h.ChangedAt)
                .IsRequired();

            entity.HasOne<Case>()
                .WithMany()
                .HasForeignKey(h => h.CaseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(h => h.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(h => new
            {
                h.OrganizationId,
                h.CaseId,
                h.ChangedAt
            });
        });

        modelBuilder.Entity<StaffMember>(entity =>
        {
            entity.ToTable("staff_members");

            entity.HasKey(s => s.Id);

            entity.Property(s => s.OrganizationId)
                .IsRequired();

            entity.Property(s => s.FullName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(s => s.Email)
                .HasMaxLength(254)
                .IsRequired();

            entity.Property(s => s.IsActive)
                .IsRequired();

            entity.Property(s => s.CreatedAt)
                .IsRequired();

            entity.Property(s => s.UpdatedAt)
                .IsRequired();

            entity.HasIndex(s => new
            {
                s.OrganizationId,
                s.Email
            }).IsUnique();

            entity.HasIndex(s => new
            {
                s.OrganizationId,
                s.IsActive
            });

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(s => s.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CaseAssignmentHistory>(entity =>
        {
            entity.ToTable("case_assignment_history");

            entity.HasKey(h => h.Id);

            entity.Property(h => h.OrganizationId)
                .IsRequired();

            entity.Property(h => h.AssignedAt)
                .IsRequired();

            entity.HasOne<Case>()
                .WithMany()
                .HasForeignKey(h => h.CaseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(h => h.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(h => new
            {
                h.OrganizationId,
                h.CaseId,
                h.AssignedAt
            });
        });

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("application_users");

            entity.HasKey(u => u.Id);

            entity.Property(u => u.FullName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(u => u.Email)
                .HasMaxLength(254)
                .IsRequired();

            entity.Property(u => u.NormalizedEmail)
                .HasMaxLength(254)
                .IsRequired();

            entity.Property(u => u.PlatformRole)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(u => u.CreatedAt)
                .IsRequired();

            entity.Property(u => u.UpdatedAt)
                .IsRequired();

            entity.HasIndex(u => u.NormalizedEmail)
                .IsUnique();
        });

        modelBuilder.Entity<OrganizationMembership>(entity =>
        {
            entity.ToTable("organization_memberships");

            entity.HasKey(m => m.Id);

            entity.Property(m => m.UserId)
                .IsRequired();

            entity.Property(m => m.OrganizationId)
                .IsRequired();

            entity.Property(m => m.Role)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(m => m.IsActive)
                .IsRequired();

            entity.Property(m => m.CreatedAt)
                .IsRequired();

            entity.Property(m => m.UpdatedAt)
                .IsRequired();

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(m => m.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(m => new
            {
                m.UserId,
                m.OrganizationId
            }).IsUnique();

            entity.HasIndex(m => new
            {
                m.OrganizationId,
                m.IsActive,
                m.Role
            });
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");

            entity.HasKey(o => o.Id);

            entity.Property(o => o.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(o => o.Code)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(o => o.IsActive)
                .IsRequired();

            entity.Property(o => o.CreatedAt)
                .IsRequired();

            entity.Property(o => o.UpdatedAt)
                .IsRequired();

            entity.HasIndex(o => o.Code)
                .IsUnique();

            entity.HasData(new
            {
                Id = Guid.Parse(
                    "11111111-1111-1111-1111-111111111111"),
                Name = "CivicFlow Development Organization",
                Code = "CIVICFLOW-DEV",
                IsActive = true,
                CreatedAt = DateTimeOffset.Parse(
                    "2026-01-01T00:00:00+00:00"),
                UpdatedAt = DateTimeOffset.Parse(
                    "2026-01-01T00:00:00+00:00")
            });
        });
    }
}
