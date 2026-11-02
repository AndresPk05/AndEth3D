using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace AndEth3D.Backend.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatTurn> ChatTurns => Set<ChatTurn>();
    public DbSet<ChatAllowance> ChatAllowances => Set<ChatAllowance>();
    public DbSet<ManufacturingRevision> ManufacturingRevisions => Set<ManufacturingRevision>();
    public DbSet<User> Users => Set<User>();
    public DbSet<GalleryItem> GalleryItems => Set<GalleryItem>();
    public DbSet<QuoteRequest> QuoteRequests => Set<QuoteRequest>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatConversation>().Property(c => c.Version).IsConcurrencyToken();
        modelBuilder.Entity<ChatConversation>().Property(c => c.ReservedUsd).HasPrecision(14, 6);
        modelBuilder.Entity<ChatConversation>().HasIndex(c => new { c.UserId, c.UpdatedAt });
        modelBuilder.Entity<ChatConversation>().HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChatTurn>().HasIndex(t => new { t.ConversationId, t.Sequence }).IsUnique();
        modelBuilder.Entity<ChatTurn>().Property(t => t.ReservedUsd).HasPrecision(14, 6);
        modelBuilder.Entity<ChatTurn>().Property(t => t.ReportedCostUsd).HasPrecision(14, 6);
        modelBuilder.Entity<ChatTurn>().HasOne<ChatConversation>().WithMany().HasForeignKey(t => t.ConversationId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ChatTurn>().HasOne<ManufacturingRevision>().WithMany().HasForeignKey(t => t.ManufacturingRevisionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChatAllowance>().HasKey(a => a.UserId);
        modelBuilder.Entity<ChatAllowance>().Property(a => a.Version).IsConcurrencyToken();
        modelBuilder.Entity<ChatAllowance>().HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
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
