using CareerVault.Api.Data.Configurations;
using CareerVault.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CareerVault.Api.Data;

public sealed class CareerVaultDbContext(DbContextOptions<CareerVaultDbContext> options) : DbContext(options)
{
    public const string Schema = "career_vault";

    public DbSet<ProfessionalEntry> ProfessionalEntries => Set<ProfessionalEntry>();
    public DbSet<ProfessionalEntryEmbedding> ProfessionalEntryEmbeddings => Set<ProfessionalEntryEmbedding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfiguration(new ProfessionalEntryConfiguration());
        modelBuilder.ApplyConfiguration(new ProfessionalEntryEmbeddingConfiguration());
    }

    public override int SaveChanges()
    {
        ApplyTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTimestamps()
    {
        var utcNow = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<ProfessionalEntry>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utcNow;
                entry.Entity.UpdatedAt = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }
        }

        foreach (var embedding in ChangeTracker.Entries<ProfessionalEntryEmbedding>())
        {
            if (embedding.State == EntityState.Added)
            {
                embedding.Entity.CreatedAt = utcNow;
            }
        }
    }
}
