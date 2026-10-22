
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Domain.Users;

namespace PublicServiceCaseManagement.Api.Infrastructure.Persistence;

public sealed class CivicFlowDbContext(
    DbContextOptions<CivicFlowDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<CaseStatusHistory> CaseStatusHistories =>
        Set<CaseStatusHistory>();
    public DbSet<CaseDocument> CaseDocuments =>
        Set<CaseDocument>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<CaseAssignmentHistory> CaseAssignmentHistories =>
        Set<CaseAssignmentHistory>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<ApplicationUser> ApplicationUsers =>
        Set<ApplicationUser>();
    public DbSet<CitizenProfile> CitizenProfiles =>
        Set<CitizenProfile>();
    public DbSet<OrganizationMembership> OrganizationMemberships =>
        Set<OrganizationMembership>();
    public DbSet<AccountInvitation> AccountInvitations =>
        Set<AccountInvitation>();
    public DbSet<PersonalAccessToken> PersonalAccessTokens =>
        Set<PersonalAccessToken>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

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

        modelBuilder.Entity<CaseDocument>(entity =>
        {
            entity.ToTable("case_documents");

            entity.HasKey(document => document.Id);

            entity.Property(document => document.OrganizationId)
                .IsRequired();

            entity.Property(document => document.OriginalFileName)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(document => document.StorageKey)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(document => document.ContentType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(document => document.SizeBytes)
                .IsRequired();

            entity.Property(document => document.Sha256Hash)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(document => document.UploadedAt)
                .IsRequired();

            entity.HasOne<Case>()
                .WithMany()
                .HasForeignKey(document => document.CaseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(document => document.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(document => new
            {
                document.OrganizationId,
                document.CaseId,
                document.UploadedAt
            });

            entity.HasIndex(document => document.StorageKey)
                .IsUnique();
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

            entity.Property(s => s.PhoneNumber)
                .HasMaxLength(PhoneNumbers.MaximumLength);

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

            entity.Property(h => h.AssignedByUserId);

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

            entity.Property(u => u.UserName)
                .HasMaxLength(254);

            entity.Property(u => u.NormalizedUserName)
                .HasMaxLength(254);

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

            entity.Property(u => u.IsActive)
                .IsRequired();

            entity.Property(u => u.CreatedAt)
                .IsRequired();

            entity.Property(u => u.UpdatedAt)
                .IsRequired();

            entity.HasIndex(u => u.NormalizedEmail)
                .IsUnique();

            entity.HasIndex(u => u.NormalizedUserName)
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

        modelBuilder.Entity<AccountInvitation>(entity =>
        {
            entity.ToTable("account_invitations");

            entity.HasKey(invitation => invitation.Id);

            entity.Property(invitation => invitation.OrganizationId)
                .IsRequired();

            entity.Property(invitation => invitation.Email)
                .HasMaxLength(254)
                .IsRequired();

            entity.Property(invitation => invitation.NormalizedEmail)
                .HasMaxLength(254)
                .IsRequired();

            entity.Property(invitation => invitation.FullName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(invitation => invitation.PhoneNumber)
                .HasMaxLength(PhoneNumbers.MaximumLength);

            entity.Property(invitation => invitation.OrganizationRole)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(invitation => invitation.TokenHash)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(invitation => invitation.CreatedByUserId)
                .IsRequired();

            entity.Property(invitation => invitation.CreatedAt)
                .IsRequired();

            entity.Property(invitation => invitation.UpdatedAt)
                .IsRequired();

            entity.Property(invitation => invitation.ExpiresAt)
                .IsRequired();

            entity.HasIndex(invitation => invitation.TokenHash)
                .IsUnique();

            entity.HasIndex(invitation => new
            {
                invitation.OrganizationId,
                invitation.NormalizedEmail
            });

            entity.HasIndex(invitation => new
            {
                invitation.OrganizationId,
                invitation.CreatedAt
            });

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(invitation => invitation.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
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
        modelBuilder.Entity<CitizenProfile>(entity =>
        {
            entity.ToTable("citizen_profiles");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.ApplicationUserId).IsRequired();
            entity.Property(c => c.FullName).HasMaxLength(150).IsRequired();
            entity.Property(c => c.Email).HasMaxLength(254).IsRequired();
            entity.Property(c => c.PhoneNumber).HasMaxLength(30);
            entity.Property(c => c.NIDNumber).HasMaxLength(50);
            entity.Property(c => c.CitizenshipNumber).HasMaxLength(50);
            entity.Property(c => c.Gender).HasMaxLength(20);
            entity.Property(c => c.PermanentAddress).HasMaxLength(500);
            entity.Property(c => c.TemporaryAddress).HasMaxLength(500);
            entity.Property(c => c.CreatedAt).IsRequired();
            entity.Property(c => c.UpdatedAt).IsRequired();
            entity.HasIndex(c => c.ApplicationUserId).IsUnique();
            entity.HasIndex(c => c.Email);
            entity.HasIndex(c => c.NIDNumber).IsUnique().HasFilter("\"NIDNumber\" IS NOT NULL");
            entity.HasIndex(c => c.CitizenshipNumber).IsUnique().HasFilter("\"CitizenshipNumber\" IS NOT NULL");
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.ApplicationUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PersonalAccessToken>(entity =>
        {
            entity.ToTable("personal_access_tokens");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.UserId).IsRequired();
            entity.Property(t => t.Name).HasMaxLength(100).IsRequired();
            entity.Property(t => t.PublicId).HasMaxLength(32).IsRequired();
            entity.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(t => t.CreatedAt).IsRequired();
            entity.HasIndex(t => t.PublicId).IsUnique();
            entity.HasIndex(t => new { t.UserId, t.CreatedAt });
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events");

            entity.HasKey(audit => audit.Id);

            entity.Property(audit => audit.Category)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(audit => audit.Action)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(audit => audit.ActorEmail)
                .HasMaxLength(254)
                .IsRequired();

            entity.Property(audit => audit.EntityType)
                .HasMaxLength(50);

            entity.Property(audit => audit.EntityId)
                .HasMaxLength(100);

            entity.Property(audit => audit.Details)
                .HasMaxLength(1000);

            entity.Property(audit => audit.OccurredAt)
                .IsRequired();

            entity.HasIndex(audit => audit.OccurredAt);

            entity.HasIndex(audit => new
            {
                audit.OrganizationId,
                audit.OccurredAt
            });

            entity.HasIndex(audit => new
            {
                audit.Category,
                audit.OccurredAt
            });
        });

        modelBuilder.Entity<IdentityRole<Guid>>(entity =>
        {
            entity.ToTable("identity_roles");
        });
        modelBuilder.Entity<IdentityUserRole<Guid>>(entity =>
        {
            entity.ToTable("identity_user_roles");
        });

        modelBuilder.Entity<IdentityUserClaim<Guid>>(entity =>
        {
            entity.ToTable("identity_user_claims");
        });

        modelBuilder.Entity<IdentityUserLogin<Guid>>(entity =>
        {
            entity.ToTable("identity_user_logins");
        });

        modelBuilder.Entity<IdentityRoleClaim<Guid>>(entity =>
        {
            entity.ToTable("identity_role_claims");
        });

        modelBuilder.Entity<IdentityUserToken<Guid>>(entity =>
        {
            entity.ToTable("identity_user_tokens");
        });
    }
}
