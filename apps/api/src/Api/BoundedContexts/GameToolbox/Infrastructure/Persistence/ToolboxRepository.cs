using Api.BoundedContexts.GameToolbox.Domain.Entities;
using Api.BoundedContexts.GameToolbox.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameToolbox.Infrastructure.Persistence;

internal class ToolboxRepository : RepositoryBase, IToolboxRepository
{

    public ToolboxRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    /// <summary>
    /// Il toolbox con strumenti e fasi, <b>tracciato</b>.
    /// </summary>
    /// <remarks>
    /// #3857 — il DbContext ha come default <c>QueryTrackingBehavior.NoTracking</c> (PERF-06),
    /// quindi il grafo restituito qui era scollegato. Gli handler aggiungevano un figlio
    /// (<c>AddPhase</c>) e chiamavano <c>UpdateAsync</c>: su un grafo scollegato
    /// <c>DbSet.Update()</c> marca <b>Modified</b> ogni entita' con la chiave valorizzata, e la
    /// fase appena creata ne ha una generata dal client. EF emetteva un UPDATE su una riga che non
    /// esisteva, 0 righe aggiornate, <c>DbUpdateConcurrencyException</c> — che l'endpoint traduceva
    /// in <b>409 concurrent_edit</b>, un conflitto inventato su un toolbox appena creato.
    ///
    /// Tracciando la lettura, il change tracker sa che quel figlio va INSERITO.
    /// Stesso schema di #3588.
    ///
    /// Dieci degli undici chiamanti sono comandi; l'unica query paga il tracciamento di un solo
    /// aggregato, che non e' un costo misurabile.
    /// </remarks>
    public async Task<Toolbox?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await DbContext.Set<Toolbox>()
            .AsTracking()
            .Include(t => t.Tools)
            .Include(t => t.Phases)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<Toolbox?> GetByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        return await DbContext.Set<Toolbox>()
            .Include(t => t.Tools)
            .Include(t => t.Phases)
            .FirstOrDefaultAsync(t => t.GameId == gameId, ct)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(Toolbox toolbox, CancellationToken ct = default)
    {
        await DbContext.Set<Toolbox>().AddAsync(toolbox, ct).ConfigureAwait(false);
    }

    public Task UpdateAsync(Toolbox toolbox, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(toolbox);

        // Su un'entita' gia' tracciata Update() e' dannoso, non ridondante: forza Modified
        // sull'INTERO grafo, compresi i figli appena aggiunti, che vanno invece inseriti (#3857).
        // Il change tracker sa gia' distinguerli. Resta per i chiamanti che passano un grafo
        // scollegato.
        if (DbContext.Entry(toolbox).State == EntityState.Detached)
        {
            DbContext.Set<Toolbox>().Update(toolbox);
        }

        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await DbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
