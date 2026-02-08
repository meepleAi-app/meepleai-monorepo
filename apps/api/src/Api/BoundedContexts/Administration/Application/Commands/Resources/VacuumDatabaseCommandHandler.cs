using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Commands.Resources;

/// <summary>
/// Handler for VacuumDatabaseCommand.
/// Executes VACUUM or VACUUM FULL command on PostgreSQL.
/// Issue #3695: Resources Monitoring - VACUUM implementation
/// </summary>
internal class VacuumDatabaseCommandHandler : ICommandHandler<VacuumDatabaseCommand, bool>
{
    private readonly MeepleAiDbContext _db;

    public VacuumDatabaseCommandHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<bool> Handle(VacuumDatabaseCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Confirmed)
        {
            throw new InvalidOperationException("Confirmation is required to execute VACUUM.");
        }

        // VACUUM cannot run inside a transaction block
        // We need to execute it directly through the connection
        var connection = _db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = request.FullVacuum ? "VACUUM FULL" : "VACUUM";

            // VACUUM can take a long time on large databases
            command.CommandTimeout = 600; // 10 minutes timeout

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            await connection.CloseAsync().ConfigureAwait(false);
        }
    }
}
