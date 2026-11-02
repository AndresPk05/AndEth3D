using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace AndEth3D.Backend.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ParametricJob> ParametricJobs => Set<ParametricJob>();
    public DbSet<ParametricAllowance> ParametricAllowances => Set<ParametricAllowance>();
    public DbSet<CalibrationRevision> CalibrationRevisions => Set<CalibrationRevision>();
    public DbSet<VisualSettingsRevision> VisualSettingsRevisions => Set<VisualSettingsRevision>();
    public DbSet<VisualProposal> VisualProposals => Set<VisualProposal>();
    public DbSet<VisualConversationUsage> VisualConversationUsages => Set<VisualConversationUsage>();
    public DbSet<VisualUserUsage> VisualUserUsages => Set<VisualUserUsage>();
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
        modelBuilder.Entity<ParametricJob>().Property(j=>j.Version).IsConcurrencyToken();
        modelBuilder.Entity<ParametricJob>().HasIndex(j=>new{j.Status,j.CreatedAt});
        modelBuilder.Entity<ParametricJob>().HasOne<ChatConversation>().WithMany().HasForeignKey(j=>j.ConversationId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ParametricJob>().HasOne<ManufacturingRevision>().WithMany().HasForeignKey(j=>j.ManufacturingRevisionId).OnDelete(DeleteBehavior.Restrict);
        foreach(var name in new[]{"Grams","Seconds","ActualGrams","ActualSeconds"})modelBuilder.Entity<ParametricJob>().Property<decimal?>(name).HasPrecision(14,4);
        modelBuilder.Entity<ParametricAllowance>().HasKey(a=>a.UserId);
        modelBuilder.Entity<ParametricAllowance>().Property(a=>a.Version).IsConcurrencyToken();
        modelBuilder.Entity<ParametricAllowance>().HasOne<User>().WithMany().HasForeignKey(a=>a.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<QuoteRequest>().HasOne<ParametricJob>().WithMany().HasForeignKey(q=>q.ParametricJobId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CalibrationRevision>().HasIndex(r => r.Version).IsUnique();
        modelBuilder.Entity<CalibrationRevision>().HasOne<ManufacturingRevision>().WithMany().HasForeignKey(r => r.ManufacturingRevisionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<QuoteRequest>().HasIndex(q => q.DesignRequestId).IsUnique();
        modelBuilder.Entity<QuoteRequest>().HasOne<VisualProposal>().WithMany().HasForeignKey(q => q.ProposalId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<QuoteRequest>().Property(q => q.ConfirmedPriceCop).HasPrecision(12,2);
        modelBuilder.Entity<QuoteRequest>().Property(q => q.ReviewVersion).IsConcurrencyToken();
        modelBuilder.Entity<VisualSettingsRevision>().HasIndex(r => r.Version).IsUnique();
        modelBuilder.Entity<VisualProposal>().Property(p => p.Version).IsConcurrencyToken();
        modelBuilder.Entity<VisualProposal>().Property(p => p.ReservedUsd).HasPrecision(14, 6);
        modelBuilder.Entity<VisualProposal>().HasIndex(p => new { p.ConversationId, p.Number }).IsUnique();
        modelBuilder.Entity<VisualProposal>().HasIndex(p => new { p.Status, p.NextAttemptAt });
        modelBuilder.Entity<VisualProposal>().HasOne<ChatConversation>().WithMany().HasForeignKey(p => p.ConversationId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VisualProposal>().HasOne<VisualProposal>().WithMany().HasForeignKey(p => p.ParentProposalId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VisualProposal>().HasOne<ManufacturingRevision>().WithMany().HasForeignKey(p => p.ManufacturingRevisionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VisualProposal>().HasOne<VisualSettingsRevision>().WithMany().HasForeignKey(p => p.SettingsRevisionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VisualConversationUsage>().HasKey(u => u.ConversationId);
        modelBuilder.Entity<VisualConversationUsage>().Property(u => u.Version).IsConcurrencyToken();
        modelBuilder.Entity<VisualConversationUsage>().Property(u => u.ChargedUsd).HasPrecision(14, 6);
        modelBuilder.Entity<VisualConversationUsage>().HasOne<ChatConversation>().WithMany().HasForeignKey(u => u.ConversationId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VisualUserUsage>().HasKey(u => u.UserId);
        modelBuilder.Entity<VisualUserUsage>().Property(u => u.Version).IsConcurrencyToken();
        modelBuilder.Entity<VisualUserUsage>().HasOne<User>().WithMany().HasForeignKey(u => u.UserId).OnDelete(DeleteBehavior.Restrict);
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
