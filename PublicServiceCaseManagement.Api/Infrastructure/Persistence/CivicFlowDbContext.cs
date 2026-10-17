using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Api.Infrastructure.Persistence;

public sealed class CivicFlowDbContext(DbContextOptions<CivicFlowDbContext> options)
    : DbContext(options)
{
    public DbSet<Case> Cases => Set<Case>();

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

            entity.Property(c => c.CreatedAt)
                .IsRequired();

            entity.Property(c => c.UpdatedAt)
                .IsRequired();

            entity.HasIndex(c => new { c.OrganizationId, c.ReferenceNumber })
                .IsUnique();

            entity.HasIndex(c => new { c.OrganizationId, c.Status });

            entity.HasIndex(c => new { c.OrganizationId, c.CreatedAt });
        });
    }
}