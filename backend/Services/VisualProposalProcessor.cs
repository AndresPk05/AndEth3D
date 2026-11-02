using System.Text.Json;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AndEth3D.Backend.Services;

public class VisualProposalProcessor(AppDbContext db, IImageGenerator generator, ProposalImageStore store, IOptions<ChatOptions> credentials)
{
    public async Task<bool> ProcessNextAsync(CancellationToken stoppingToken)
    {
        var now = DateTime.UtcNow;
        var candidates = await db.VisualProposals.Where(p =>
            (p.Status == "Queued" && p.NextAttemptAt <= now) || (p.Status == "Processing" && p.LeaseUntil <= now))
            .OrderBy(p => p.NextAttemptAt).Take(20).ToListAsync(stoppingToken);
        foreach (var proposal in candidates) {
            if (proposal.Status == "Processing") {
                var records = VisualPolicy.Attempts(proposal);
                if (records.Count > 0) { records[^1].Status = "Interrupted"; records[^1].CompletedAt = now; }
                proposal.AttemptsJson = JsonSerializer.Serialize(records, DesignPolicy.Json);
                await Finish(proposal, "Interrupted", null);
                return true;
            }
            var conversation = await db.ChatConversations.AsNoTracking().SingleAsync(c => c.Id == proposal.ConversationId, stoppingToken);
            if (conversation.BusyUntil > now) continue;
            var latest = await db.VisualSettingsRevisions.AsNoTracking().OrderByDescending(r => r.Version).FirstAsync(stoppingToken);
            if (!VisualPolicy.Settings(latest).Enabled || string.IsNullOrWhiteSpace(credentials.Value.ApiKey)) {
                await Finish(proposal, "Disabled", null); return true;
            }
            var manufacturing = await db.ManufacturingRevisions.AsNoTracking().OrderByDescending(r => r.Version).FirstOrDefaultAsync(stoppingToken);
            if (VisualPolicy.Fingerprint(conversation.BriefJson) != proposal.BriefFingerprint || manufacturing?.Id != proposal.ManufacturingRevisionId ||
                DesignPolicy.Assess(DesignPolicy.Read(proposal.BriefJson), ManufacturingService.Read(manufacturing!)).Status != "ReadyForReview") {
                await Finish(proposal, "Stale", null); return true;
            }
            var settings = VisualPolicy.Settings(await db.VisualSettingsRevisions.AsNoTracking().SingleAsync(r => r.Id == proposal.SettingsRevisionId, stoppingToken));
            var attempts = VisualPolicy.Attempts(proposal);
            proposal.Status = "Processing"; proposal.AttemptCount++; proposal.Version++;
            proposal.LeaseUntil = now.AddSeconds(settings.TimeoutSeconds + 30);
            var attempt = new VisualAttempt { Number = proposal.AttemptCount, StartedAt = now, ReservedUsd = settings.ReservePerAttemptUsd };
            attempts.Add(attempt); proposal.AttemptsJson = JsonSerializer.Serialize(attempts, DesignPolicy.Json);
            try { await db.SaveChangesAsync(stoppingToken); }
            catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return true; }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            string? savedFile = null;
            bool retry = false;
            string? failure = null;
            try {
                byte[]? reference = null;
                if (proposal.ParentProposalId is not null) {
                    var parent = await db.VisualProposals.AsNoTracking().SingleAsync(p => p.Id == proposal.ParentProposalId && p.ConversationId == proposal.ConversationId && p.Status == "Completed", timeout.Token);
                    reference = await store.ReadAsync(parent.ImageFile!, timeout.Token);
                }
                var result = await generator.GenerateAsync(settings, proposal.Prompt, reference, timeout.Token).WaitAsync(timeout.Token);
                attempt.ReportedCostUsd = result.CostUsd;
                savedFile = await store.SaveAsync(result.Bytes, timeout.Token);
                proposal.ImageFile = savedFile;
                attempt.Status = "Completed";
            }
            catch (ImageProviderException ex) {
                attempt.ReportedCostUsd = ex.CostUsd; failure = "Rejected";
                retry = ex.Retryable && proposal.AttemptCount < settings.MaxAttempts && !stoppingToken.IsCancellationRequested;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException or JsonException or InvalidOperationException or ArgumentException) {
                // Timeouts/connection loss may have been billed: no automatic duplicate submission.
                failure = ex is OperationCanceledException ? "Interrupted" : "Failed";
            }
            attempt.CompletedAt = DateTime.UtcNow;
            if (failure is not null) attempt.Status = failure;
            proposal.AttemptsJson = JsonSerializer.Serialize(attempts, DesignPolicy.Json);
            // Unknown costs retain the full reservation; known overruns are charged additionally.
            var overage = Math.Max(0, (attempt.ReportedCostUsd ?? 0) - settings.ReservePerAttemptUsd);
            if (retry && overage == 0) {
                proposal.Status = "Queued"; proposal.LeaseUntil = null; proposal.Version++;
                proposal.NextAttemptAt = DateTime.UtcNow.AddSeconds(5 * proposal.AttemptCount);
                try { await db.SaveChangesAsync(CancellationToken.None); }
                catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); }
            } else {
                if (!await Finish(proposal, failure, overage) && savedFile is not null) store.Delete(savedFile);
            }
            return true;
        }
        return false;
    }

    private async Task<bool> Finish(VisualProposal proposal, string? failure, decimal? overage)
    {
        var usage = await db.VisualConversationUsages.SingleAsync(u => u.ConversationId == proposal.ConversationId);
        if (usage.ActiveProposalId != proposal.Id) { db.ChangeTracker.Clear(); return false; }
        usage.ActiveProposalId = null; usage.Version++; usage.ChargedUsd += overage ?? 0;
        proposal.Status = failure is null ? "Completed" : "Failed";
        proposal.FailureCode = failure; proposal.CompletedAt = DateTime.UtcNow; proposal.LeaseUntil = null; proposal.Version++;
        try { await db.SaveChangesAsync(CancellationToken.None); return true; }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return false; }
    }
}

public class VisualProposalWorker(IServiceScopeFactory scopes, ILogger<VisualProposalWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                using var scope = scopes.CreateScope();
                var processed = await scope.ServiceProvider.GetRequiredService<VisualProposalProcessor>().ProcessNextAsync(stoppingToken);
                if (processed) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Visual worker could not process a job ({ErrorType}); retrying queue polling.", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
