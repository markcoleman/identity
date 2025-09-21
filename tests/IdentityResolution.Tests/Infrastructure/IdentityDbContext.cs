using IdentityResolution.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityResolution.Tests.Infrastructure;

/// <summary>
/// Entity Framework DbContext for integration testing with PostgreSQL
/// </summary>
public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<Identity> Identities { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Identity entity
        modelBuilder.Entity<Identity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.Source).HasMaxLength(100);
            entity.Property(e => e.Confidence).HasPrecision(5, 3);

            // Personal Info as owned entity
            entity.OwnsOne(e => e.PersonalInfo, pi =>
            {
                pi.Property(p => p.FirstName).HasMaxLength(100);
                pi.Property(p => p.MiddleName).HasMaxLength(100);
                pi.Property(p => p.LastName).HasMaxLength(100);
                pi.Property(p => p.FullName).HasMaxLength(300);
                pi.Property(p => p.Gender).HasMaxLength(20);

                // Address as nested owned entity
                pi.OwnsOne(p => p.Address, addr =>
                {
                    addr.Property(a => a.Street1).HasMaxLength(200);
                    addr.Property(a => a.Street2).HasMaxLength(200);
                    addr.Property(a => a.City).HasMaxLength(100);
                    addr.Property(a => a.State).HasMaxLength(50);
                    addr.Property(a => a.PostalCode).HasMaxLength(20);
                    addr.Property(a => a.Country).HasMaxLength(50);
                });
            });

            // Contact Info as owned entity
            entity.OwnsOne(e => e.ContactInfo, ci =>
            {
                ci.Property(c => c.Email).HasMaxLength(200);
                ci.Property(c => c.Phone).HasMaxLength(50);
                ci.Property(c => c.AlternatePhone).HasMaxLength(50);
                ci.Property(c => c.Website).HasMaxLength(500);
            });

            // Identifiers as JSON column
            entity.Property(e => e.Identifiers)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<Identifier>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<Identifier>())
                .HasColumnType("jsonb");

            // Attributes as JSON column
            entity.Property(e => e.Attributes)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, string>())
                .HasColumnType("jsonb");

            // Indexes for performance
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Source);
        });
    }
}