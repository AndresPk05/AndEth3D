using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace AndEth3D.Backend.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ManufacturingRevision> ManufacturingRevisions => Set<ManufacturingRevision>();
    public DbSet<User> Users => Set<User>();
    public DbSet<GalleryItem> GalleryItems => Set<GalleryItem>();
    public DbSet<QuoteRequest> QuoteRequests => Set<QuoteRequest>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ManufacturingRevision>().HasIndex(r => r.Version).IsUnique();
        modelBuilder.Entity<QuoteRequest>().HasOne<ManufacturingRevision>().WithMany()
            .HasForeignKey(q => q.ManufacturingRevisionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Order>().HasIndex(o => o.QuoteRequestId).IsUnique();
        modelBuilder.Entity<QuoteRequest>().Property(q => q.Status).IsConcurrencyToken();
        modelBuilder.Entity<Order>().Property(o => o.Status).IsConcurrencyToken();
        modelBuilder.Entity<Order>()
            .Property(o => o.Price)
            .HasPrecision(12, 2);
    }
}
