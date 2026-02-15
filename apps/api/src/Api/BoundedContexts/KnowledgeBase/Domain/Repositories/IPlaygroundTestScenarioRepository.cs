using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for PlaygroundTestScenario persistence.
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
public interface IPlaygroundTestScenarioRepository
{
    Task<PlaygroundTestScenario?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<PlaygroundTestScenario>> GetAllAsync(ScenarioCategory? category = null, Guid? agentDefinitionId = null, bool activeOnly = true, CancellationToken ct = default);
    Task AddAsync(PlaygroundTestScenario scenario, CancellationToken ct = default);
    Task UpdateAsync(PlaygroundTestScenario scenario, CancellationToken ct = default);
}
