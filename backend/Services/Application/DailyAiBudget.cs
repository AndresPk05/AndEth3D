using AndEth3D.Backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AndEth3D.Backend.Services;

internal static class DailyAiBudget
{
    public static async Task<IDbContextTransaction> Begin(AppDbContext db)
    {
        var transaction = await db.Database.BeginTransactionAsync();
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(172349108)");
        return transaction;
    }

    public static async Task<bool> Fits(AppDbContext db, decimal reservation, decimal limit)
    {
        var today = DateTime.UtcNow.Date;
        var chat = await db.ChatTurns.Where(t => t.CreatedAt >= today)
            .SumAsync(t => t.ReportedCostUsd > t.ReservedUsd ? t.ReportedCostUsd.Value : t.ReservedUsd);
        var images = await db.VisualProposals.Where(p => p.CreatedAt >= today).SumAsync(p => p.ReservedUsd);
        return chat + images + reservation <= limit;
    }
}
