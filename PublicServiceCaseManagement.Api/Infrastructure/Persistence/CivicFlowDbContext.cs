using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Staff;

namespace PublicServiceCaseManagement.Api.Infrastructure.Persistence;

public sealed class CivicFlowDbContext(DbContextOptions<CivicFlowDbContext> options)
    : DbContext(options)
{
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<CaseStatusHistory> CaseStatusHistories => Set<CaseStatusHistory>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<CaseAssignmentHistory> CaseAssignmentHistories =>
        Set<CaseAssignmentHistory>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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

            entity.HasIndex(c => new
            {
                c.OrganizationId,
                c.AssignedToStaffMemberId
            });

            entity.Property(c => c.CreatedAt)
                .IsRequired();

            entity.Property(c => c.UpdatedAt)
                .IsRequired();

            entity.HasIndex(c => new { c.OrganizationId, c.ReferenceNumber })
                .IsUnique();

            entity.HasIndex(c => new { c.OrganizationId, c.Status });

            entity.HasIndex(c => new { c.OrganizationId, c.CreatedAt });
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

            entity.HasIndex(s => new { s.OrganizationId, s.Email })
                .IsUnique();

            entity.HasIndex(s => new { s.OrganizationId, s.IsActive });
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

            entity.HasIndex(h => new
            {
                h.OrganizationId,
                h.CaseId,
                h.AssignedAt
            });
        });



    }
}