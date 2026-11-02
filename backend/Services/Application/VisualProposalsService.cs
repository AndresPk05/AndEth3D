using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AndEth3D.Backend.Services;
public class VisualProposalsService(AppDbContext db, IOptions<ChatOptions> credentials, ProposalImageStore images, ICurrentActor actor)
{
    private int UserId => actor.Id;
    public async Task<AppResult> List(Guid conversationId)
    {
        var conversation = await db.ChatConversations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == conversationId && c.UserId == UserId);
        if (conversation is null) return AppResult.NotFound();
        var configRevision = await db.VisualSettingsRevisions.AsNoTracking().OrderByDescending(r => r.Version).FirstOrDefaultAsync();
        var config = configRevision is null ? new() : VisualPolicy.Settings(configRevision);
        var manufacturing = await db.ManufacturingRevisions.AsNoTracking().OrderByDescending(r => r.Version).FirstOrDefaultAsync();
        var usage = await db.VisualConversationUsages.AsNoTracking().SingleOrDefaultAsync(u => u.ConversationId == conversationId);
        var fingerprint = VisualPolicy.Fingerprint(conversation.BriefJson);
        var ready = manufacturing is not null && DesignPolicy.Assess(DesignPolicy.Read(conversation.BriefJson), ManufacturingService.Read(manufacturing)).Status == "ReadyForReview";
        var enabled = config.Enabled && !string.IsNullOrWhiteSpace(credentials.Value.ApiKey);
        var budget = (usage?.ChargedUsd ?? 0) + config.ReservePerAttemptUsd * config.MaxAttempts <= config.ConversationBudgetUsd &&
            (usage?.ProposalCount ?? 0) < config.MaxProposalsPerConversation;
        var proposals = await db.VisualProposals.AsNoTracking().Where(p => p.ConversationId == conversationId).OrderByDescending(p => p.Number).ToListAsync();
        var revisionIds = proposals.Select(p => p.ManufacturingRevisionId).Distinct().ToArray();
        var historicalConfigurations = (await db.ManufacturingRevisions.AsNoTracking().Where(r => revisionIds.Contains(r.Id)).ToListAsync())
            .ToDictionary(r => r.Id, ManufacturingService.Read);
        return AppResult.Ok(new {
            enabled, supportsEditing = config.SupportsEditing, fingerprint,
            canGenerate = enabled && ready && budget && usage?.ActiveProposalId is null && !(conversation.BusyUntil > DateTime.UtcNow),
            reason = !enabled ? "La generación de imágenes no está disponible por ahora." : !ready ? "Completa la ficha y resuelve los detalles pendientes para generar una imagen." :
                !budget ? "Alcanzaste el límite de propuestas de esta conversación." : usage?.ActiveProposalId is not null ? "Tu propuesta está en preparación." : "Puedes generar una vista conceptual de tu ficha actual.",
            proposals = proposals.Select(p => new { p.Id, p.Number, p.ParentProposalId, p.Status, p.CreatedAt, p.CompletedAt,
                outdated = p.BriefFingerprint != fingerprint || p.ManufacturingRevisionId != manufacturing?.Id,
                brief = DesignPolicy.Read(p.BriefJson),
                materialName = historicalConfigurations[p.ManufacturingRevisionId].Materials.FirstOrDefault(m => m.Id == DesignPolicy.Read(p.BriefJson).MaterialId)?.Name ?? "Material seleccionado",
                imageUrl = p.Status == "Completed" ? $"/api/chat/conversations/{conversationId}/visuals/{p.Id}/image" : null,
                message = p.Status == "Failed" ? VisualPolicy.FailureMessage(p.FailureCode) : p.Status == "Completed" ? "Vista conceptual; requiere validación técnica." : "Preparando tu propuesta…"
            })
        });
    }
    public async Task<AppResult> Create(Guid conversationId, CreateVisualProposalRequest request)
    {
        var conversation = await db.ChatConversations.SingleOrDefaultAsync(c => c.Id == conversationId && c.UserId == UserId);
        if (conversation is null) return AppResult.NotFound();
        if (request.RequestId == Guid.Empty) return AppResult.Invalid();
        var previous = await db.VisualProposals.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.RequestId);
        if (previous is not null) return previous.ConversationId == conversationId && previous.BriefFingerprint == request.ExpectedFingerprint && previous.ParentProposalId == request.ParentProposalId
            ? AppResult.Ok(new { id = previous.Id }) : AppResult.Conflict(new { message = "Este identificador corresponde a otra solicitud." });
        var settingsRevision = await db.VisualSettingsRevisions.AsNoTracking().OrderByDescending(r => r.Version).FirstOrDefaultAsync();
        var settings = settingsRevision is null ? new() : VisualPolicy.Settings(settingsRevision);
        if (!settings.Enabled || string.IsNullOrWhiteSpace(credentials.Value.ApiKey)) return AppResult.Unavailable(new { message = "La generación de imágenes está pausada." });
        var fingerprint = VisualPolicy.Fingerprint(conversation.BriefJson);
        if (conversation.BusyUntil > DateTime.UtcNow || fingerprint != request.ExpectedFingerprint)
            return AppResult.Conflict(new { message = "La ficha cambió o se está actualizando. Revísala antes de generar la imagen." });
        var manufacturing = await db.ManufacturingRevisions.AsNoTracking().OrderByDescending(r => r.Version).FirstOrDefaultAsync();
        var brief = DesignPolicy.Read(conversation.BriefJson);
        if (manufacturing is null || DesignPolicy.Assess(brief, ManufacturingService.Read(manufacturing)).Status != "ReadyForReview")
            return AppResult.Invalid(new { message = "Completa la ficha y ajusta las opciones incompatibles antes de generar una propuesta." });
        if (request.ParentProposalId is not null) {
            if (!settings.SupportsEditing) return AppResult.Invalid(new { message = "El modelo configurado no admite editar una imagen anterior." });
            var parent = await db.VisualProposals.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.ParentProposalId && p.ConversationId == conversationId && p.Status == "Completed");
            if (parent is null) return AppResult.NotFound();
        }
        var usage = await db.VisualConversationUsages.SingleOrDefaultAsync(u => u.ConversationId == conversationId);
        if (usage is null) { usage = new() { ConversationId = conversationId }; db.VisualConversationUsages.Add(usage); }
        if (usage.ActiveProposalId is not null) return AppResult.Conflict(new { message = "Ya hay una propuesta en preparación para esta conversación." });
        var reservation = settings.ReservePerAttemptUsd * settings.MaxAttempts;
        if (usage.ProposalCount >= settings.MaxProposalsPerConversation || usage.ChargedUsd + reservation > settings.ConversationBudgetUsd)
            return AppResult.RateLimited(new { message = "Esta conversación alcanzó su límite de generación de imágenes." });
        var daily = await db.VisualUserUsages.SingleOrDefaultAsync(u => u.UserId == UserId);
        if (daily is null) { daily = new() { UserId = UserId }; db.VisualUserUsages.Add(daily); }
        if (daily.Day != DateTime.UtcNow.Date) { daily.Day = DateTime.UtcNow.Date; daily.ReservedAttempts = 0; }
        if (daily.ReservedAttempts + settings.MaxAttempts > settings.MaxAttemptsPerUserDay)
            return AppResult.RateLimited(new { message = "Alcanzaste el límite diario de imágenes. Tus propuestas siguen guardadas." });
        var proposal = new VisualProposal { Id = request.RequestId, ConversationId = conversationId, Number = usage.ProposalCount + 1,
            ParentProposalId = request.ParentProposalId, ManufacturingRevisionId = manufacturing.Id, SettingsRevisionId = settingsRevision!.Id,
            BriefJson = DesignPolicy.Write(brief), BriefFingerprint = fingerprint, Prompt = VisualPolicy.Prompt(brief, ManufacturingService.Read(manufacturing)), ReservedUsd = reservation };
        db.VisualProposals.Add(proposal);
        usage.Version++; usage.ProposalCount++; usage.ChargedUsd += reservation; usage.ActiveProposalId = proposal.Id;
        daily.Version++; daily.ReservedAttempts += settings.MaxAttempts;
        // Compare with concurrent chat mutations; no model call occurs inside this transaction.
        conversation.Version++;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return AppResult.Conflict(new { message = "La conversación cambió. Recarga e intenta de nuevo." }); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { return AppResult.Conflict(new { message = "Otra solicitud se guardó al mismo tiempo. Recarga antes de continuar." }); }
        return AppResult.Accepted(new { id = proposal.Id });
    }
    public async Task<AppResult> Image(Guid conversationId, Guid id)
    {
        if (!await db.ChatConversations.AnyAsync(c => c.Id == conversationId && c.UserId == UserId)) return AppResult.NotFound();
        var proposal = await db.VisualProposals.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.ConversationId == conversationId && p.Status == "Completed");
        var path = images.Resolve(proposal?.ImageFile);
        if (path is null || !System.IO.File.Exists(path)) return AppResult.NotFound();
        return AppResult.File(new DownloadFile(path, "image/png"));
    }
}
