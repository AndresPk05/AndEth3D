using AndEth3D.Backend.Contracts;
using System.Text.Json;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AndEth3D.Backend.Services;
public class ChatService(AppDbContext db, IDesignAgent agent, IOptions<ChatOptions> settings, ICurrentActor actor)
{
    private int UserId => actor.Id;
    private ChatOptions Options => settings.Value;
    private const string FailedReply = "No pude procesar este mensaje. Tu ficha anterior está guardada. Puedes volver a enviarlo o continuar más tarde.";
    public AppResult Status() => AppResult.Ok(new { enabled = Options.Enabled, maxMessageLength = 2000, maxTurns = Options.MaxTurns });
    public async Task<AppResult> List(int skip = 0) => AppResult.Ok(await db.ChatConversations.AsNoTracking()
        .Where(c => c.UserId == UserId).OrderByDescending(c => c.UpdatedAt).Skip(skip).Take(30)
        .Select(c => new { c.Id, c.Title, c.UpdatedAt }).ToListAsync());

    private async Task<ChatAllowance> Allowance()
    {
        var allowance = await db.ChatAllowances.SingleOrDefaultAsync(a => a.UserId == UserId);
        if (allowance is null) { allowance = new() { UserId = UserId }; db.ChatAllowances.Add(allowance); }
        if (allowance.Day != DateTime.UtcNow.Date) {
            allowance.Day = DateTime.UtcNow.Date; allowance.Conversations = 0; allowance.Requests = 0;
        }
        return allowance;
    }
    public async Task<AppResult> Create()
    {
        if (!Options.Enabled) return AppResult.Unavailable(new { message = "El asistente aún no está disponible. Puedes usar el formulario de cotización." });
        var allowance = await Allowance();
        if (allowance.Conversations >= Options.MaxConversationsPerDay) return AppResult.RateLimited(new { message = "Alcanzaste el límite diario de nuevas ideas. Puedes retomar una conversación existente." });
        allowance.Conversations++; allowance.Version++;
        var conversation = new ChatConversation { UserId = UserId };
        db.ChatConversations.Add(conversation);
        if (!await SaveReservation()) return AppResult.Conflict(new { message = "Otra solicitud está en curso. Intenta de nuevo." });
        return AppResult.Ok(new { conversation.Id });
    }

    private async Task<(ManufacturingRevision? Revision, ManufacturingConfiguration Config)> Configuration()
    {
        var revision = await db.ManufacturingRevisions.AsNoTracking().OrderByDescending(r => r.Version).FirstOrDefaultAsync();
        return (revision, revision is null ? new() : ManufacturingService.Read(revision));
    }
    public async Task<AppResult> Get(Guid id)
    {
        var c = await db.ChatConversations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && c.UserId == UserId);
        if (c is null) return AppResult.NotFound();
        var turns = await db.ChatTurns.AsNoTracking().Where(t => t.ConversationId == id).OrderBy(t => t.Sequence).ToListAsync();
        var (_, config) = await Configuration();
        var brief = DesignPolicy.Read(c.BriefJson);
        return AppResult.Ok(new {
            c.Id, c.Title, brief, assessment = DesignPolicy.Assess(brief, config),
            busy = c.BusyUntil > DateTime.UtcNow,
            remainingTurns = Math.Max(0, Options.MaxTurns - c.Attempts),
            budgetAvailable = c.ReservedUsd + Options.ReservationUsd <= Options.SessionBudgetUsd,
            messages = turns.Select(t => new { t.Id, t.UserText,
                assistantText = t.Status == "Processing" && (t.Sequence < c.Attempts || !(c.BusyUntil > DateTime.UtcNow)) ? FailedReply : t.AssistantText,
                status = t.Status == "Processing" && (t.Sequence < c.Attempts || !(c.BusyUntil > DateTime.UtcNow)) ? "Failed" : t.Status })
        });
    }
    public async Task<AppResult> Send(Guid id, SendChatMessageRequest request)
    {
        var c = await db.ChatConversations.SingleOrDefaultAsync(c => c.Id == id && c.UserId == UserId);
        if (c is null) return AppResult.NotFound();
        if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.Message)) return AppResult.Invalid();
        var previous = await db.ChatTurns.AsNoTracking().SingleOrDefaultAsync(t => t.Id == request.RequestId);
        if (previous is not null) return previous.ConversationId == id ? await Get(id) : AppResult.Conflict(new { message = "Identificador de mensaje ya utilizado." });
        if (!Options.Enabled) return AppResult.Unavailable(new { message = "El asistente no está disponible. Tu conversación sigue guardada." });
        var now = DateTime.UtcNow;
        if (c.BusyUntil > now) return AppResult.Conflict(new { message = "Tu mensaje anterior todavía se está procesando." });
        var allowance = await Allowance();
        if (allowance.Requests >= Options.MaxRequestsPerDay || allowance.LastRequestAt?.AddSeconds(Options.MinimumIntervalSeconds) > now)
            return AppResult.RateLimited(new { message = "Alcanzaste el límite de uso o enviaste mensajes muy rápido. Intenta más tarde." });
        if (c.Attempts >= Options.MaxTurns || c.ReservedUsd + Options.ReservationUsd > Options.SessionBudgetUsd)
            return AppResult.RateLimited(new { message = "Esta conversación alcanzó su límite. Tu ficha está guardada y puedes solicitar asesoría desde Cotizar." });
        var (revision, config) = await Configuration();
        var history = await db.ChatTurns.AsNoTracking().Where(t => t.ConversationId == id && t.Status == "Completed").OrderBy(t => t.Sequence).ToListAsync();
        // Reservation is committed BEFORE an external call and is never refunded on uncertain failures.
        var turn = new ChatTurn { Id = request.RequestId, ConversationId = id, Sequence = c.Attempts + 1,
            UserText = request.Message.Trim(), ReservedUsd = Options.ReservationUsd, ManufacturingRevisionId = revision?.Id };
        db.ChatTurns.Add(turn);
        c.Attempts++; c.ReservedUsd += turn.ReservedUsd; c.Version++; c.UpdatedAt = now;
        c.BusyUntil = now.AddSeconds(Options.TimeoutSeconds + 30);
        if (c.Attempts == 1) c.Title = turn.UserText[..Math.Min(80, turn.UserText.Length)];
        allowance.Requests++; allowance.Version++; allowance.LastRequestAt = now;
        if (!await SaveReservation()) return AppResult.Conflict(new { message = "Otra solicitud está en curso. Recarga la conversación." });
        // Independent of browser cancellation: persist the outcome even if the client disconnects.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Options.TimeoutSeconds));
        try {
            var before = DesignPolicy.Read(c.BriefJson);
            var result = await agent.CompleteAsync(before, DesignPolicy.PublicCapabilities(config), history, turn.UserText, timeout.Token).WaitAsync(timeout.Token);
            var updated = DesignPolicy.Merge(before, result.Patch);
            var assessment = DesignPolicy.Assess(updated, config);
            c.BriefJson = DesignPolicy.Write(updated); turn.BriefJson = c.BriefJson;
            turn.AssistantText = (result.Patch.OffTopic ? "Puedo ayudarte a definir objetos para impresión 3D. " : "") + assessment.Question;
            turn.Status = "Completed"; turn.Model = result.Model; turn.ReportedCostUsd = result.CostUsd;
            if (result.CostUsd > turn.ReservedUsd) c.ReservedUsd = Math.Max(c.ReservedUsd, Options.SessionBudgetUsd);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidDataException or JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException or IndexOutOfRangeException)
        {
            turn.Status = "Failed"; turn.AssistantText = FailedReply;
        }
        c.BusyUntil = null; c.UpdatedAt = DateTime.UtcNow; c.Version++;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) {
            // A lease expired and another turn owns the conversation. Never overwrite its newer brief.
            db.ChangeTracker.Clear();
            return AppResult.Conflict(new { message = "La conversación cambió mientras se procesaba. Recarga para ver su estado." });
        }
        return await Get(id);
    }

    private async Task<bool> SaveReservation()
    {
        try { await db.SaveChangesAsync(); return true; }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return false; }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { db.ChangeTracker.Clear(); return false; }
    }
}
