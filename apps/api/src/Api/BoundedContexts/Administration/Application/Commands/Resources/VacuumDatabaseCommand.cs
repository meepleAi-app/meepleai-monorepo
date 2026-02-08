using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands.Resources;

/// <summary>
/// Command to execute VACUUM on PostgreSQL database.
/// WARNING: This operation will briefly lock tables during cleanup.
/// Issue #3695: Resources Monitoring - VACUUM database action (Level 1 confirmation required)
/// </summary>
internal record VacuumDatabaseCommand(
    bool Confirmed = false,
    bool FullVacuum = false // FULL VACUUM is more thorough but locks tables longer
) : ICommand<bool>;
