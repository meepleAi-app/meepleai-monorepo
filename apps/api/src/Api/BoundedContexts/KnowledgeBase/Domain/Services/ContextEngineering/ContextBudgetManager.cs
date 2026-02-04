namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;

/// <summary>
/// Manages adaptive context window budget allocation across multiple sources.
/// Issue #3491: Context Engineering Framework Implementation.
/// </summary>
/// <remarks>
/// Implements priority-based token allocation that respects:
/// - Source priorities (higher priority = larger allocation)
/// - Minimum allocations per source
/// - Maximum context window limits
/// - Dynamic reallocation when sources underutilize their budget
/// </remarks>
public sealed class ContextBudgetManager
{
    private readonly int _totalBudget;
    private readonly Dictionary<string, SourceBudget> _sourceBudgets = new(StringComparer.Ordinal);

    public ContextBudgetManager(int totalBudget)
    {
        if (totalBudget <= 0)
            throw new ArgumentException("Total budget must be positive", nameof(totalBudget));

        _totalBudget = totalBudget;
    }

    /// <summary>
    /// Gets the total token budget for the context window.
    /// </summary>
    public int TotalBudget => _totalBudget;

    /// <summary>
    /// Gets the remaining unallocated tokens.
    /// </summary>
    public int RemainingBudget => _totalBudget - _sourceBudgets.Values.Sum(b => b.AllocatedTokens);

    /// <summary>
    /// Registers a context source with its priority and constraints.
    /// </summary>
    /// <param name="sourceId">Unique identifier for the source.</param>
    /// <param name="priority">Priority level (0-100, higher = more important).</param>
    /// <param name="minTokens">Minimum tokens to allocate.</param>
    /// <param name="maxTokens">Maximum tokens to allocate (0 = no limit).</param>
    public void RegisterSource(
        string sourceId,
        int priority,
        int minTokens = 0,
        int maxTokens = 0)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("Source ID cannot be empty", nameof(sourceId));
        if (priority < 0 || priority > 100)
            throw new ArgumentException("Priority must be between 0 and 100", nameof(priority));
        if (minTokens < 0)
            throw new ArgumentException("Minimum tokens cannot be negative", nameof(minTokens));
        if (maxTokens < 0)
            throw new ArgumentException("Maximum tokens cannot be negative", nameof(maxTokens));

        _sourceBudgets[sourceId] = new SourceBudget
        {
            SourceId = sourceId,
            Priority = priority,
            MinTokens = minTokens,
            MaxTokens = maxTokens > 0 ? maxTokens : _totalBudget,
            AllocatedTokens = 0,
            UsedTokens = 0
        };
    }

    /// <summary>
    /// Calculates and returns the token allocation for each registered source.
    /// </summary>
    /// <returns>Dictionary of source ID to allocated token count.</returns>
    public IReadOnlyDictionary<string, int> CalculateAllocations()
    {
        if (_sourceBudgets.Count == 0)
            return new Dictionary<string, int>(StringComparer.Ordinal);

        // Step 1: Ensure minimum allocations
        var allocations = new Dictionary<string, int>(StringComparer.Ordinal);
        var remainingBudget = _totalBudget;

        foreach (var budget in _sourceBudgets.Values)
        {
            var minAlloc = Math.Min(budget.MinTokens, remainingBudget);
            allocations[budget.SourceId] = minAlloc;
            remainingBudget -= minAlloc;
        }

        // Step 2: Distribute remaining budget by priority
        if (remainingBudget > 0)
        {
            var totalPriority = _sourceBudgets.Values.Sum(b => b.Priority);
            if (totalPriority == 0)
                totalPriority = _sourceBudgets.Count; // Equal distribution if no priorities

            foreach (var budget in _sourceBudgets.Values.OrderByDescending(b => b.Priority))
            {
                var share = totalPriority > 0
                    ? (int)(remainingBudget * ((double)budget.Priority / totalPriority))
                    : remainingBudget / _sourceBudgets.Count;

                var currentAlloc = allocations[budget.SourceId];
                var newAlloc = Math.Min(currentAlloc + share, budget.MaxTokens);
                var actualIncrease = newAlloc - currentAlloc;

                allocations[budget.SourceId] = newAlloc;
                remainingBudget -= actualIncrease;
            }
        }

        // Step 3: Distribute any remaining tokens to highest priority sources
        var sortedByPriority = _sourceBudgets.Values
            .OrderByDescending(b => b.Priority)
            .ToList();

        while (remainingBudget > 0 && sortedByPriority.Count > 0)
        {
            var distributed = false;
            foreach (var budget in sortedByPriority)
            {
                if (allocations[budget.SourceId] < budget.MaxTokens)
                {
                    var additionalTokens = Math.Min(
                        remainingBudget,
                        budget.MaxTokens - allocations[budget.SourceId]);
                    allocations[budget.SourceId] += additionalTokens;
                    remainingBudget -= additionalTokens;
                    distributed = true;

                    if (remainingBudget <= 0) break;
                }
            }
            if (!distributed) break;
        }

        // Update internal state
        foreach (var kvp in allocations)
        {
            if (_sourceBudgets.TryGetValue(kvp.Key, out var budget))
            {
                budget.AllocatedTokens = kvp.Value;
            }
        }

        return allocations;
    }

    /// <summary>
    /// Records actual token usage for a source and returns any unused allocation.
    /// </summary>
    /// <param name="sourceId">The source that used tokens.</param>
    /// <param name="usedTokens">Number of tokens actually used.</param>
    /// <returns>Number of unused tokens that can be reallocated.</returns>
    public int RecordUsage(string sourceId, int usedTokens)
    {
        if (!_sourceBudgets.TryGetValue(sourceId, out var budget))
            throw new ArgumentException($"Unknown source: {sourceId}", nameof(sourceId));

        budget.UsedTokens = Math.Min(usedTokens, budget.AllocatedTokens);
        return Math.Max(0, budget.AllocatedTokens - budget.UsedTokens);
    }

    /// <summary>
    /// Reallocates unused tokens from completed sources to pending ones.
    /// </summary>
    /// <param name="completedSourceIds">Sources that have finished retrieval.</param>
    /// <returns>Updated allocations for remaining sources.</returns>
    public IReadOnlyDictionary<string, int> ReallocateUnused(IEnumerable<string> completedSourceIds)
    {
        var completed = completedSourceIds.ToHashSet(StringComparer.Ordinal);
        var unusedTokens = 0;

        // Collect unused tokens
        foreach (var sourceId in completed)
        {
            if (_sourceBudgets.TryGetValue(sourceId, out var budget))
            {
                unusedTokens += Math.Max(0, budget.AllocatedTokens - budget.UsedTokens);
            }
        }

        if (unusedTokens == 0)
            return _sourceBudgets
                .Where(kvp => !completed.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.AllocatedTokens, StringComparer.Ordinal);

        // Reallocate to pending sources by priority
        var pendingSources = _sourceBudgets.Values
            .Where(b => !completed.Contains(b.SourceId))
            .OrderByDescending(b => b.Priority)
            .ToList();

        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var budget in pendingSources)
        {
            var additional = Math.Min(
                unusedTokens,
                budget.MaxTokens - budget.AllocatedTokens);

            budget.AllocatedTokens += additional;
            unusedTokens -= additional;
            result[budget.SourceId] = budget.AllocatedTokens;
        }

        return result;
    }

    /// <summary>
    /// Gets the current budget status for all sources.
    /// </summary>
    public IReadOnlyList<BudgetStatus> GetStatus()
    {
        return _sourceBudgets.Values
            .Select(b => new BudgetStatus(
                b.SourceId,
                b.Priority,
                b.AllocatedTokens,
                b.UsedTokens,
                b.AllocatedTokens - b.UsedTokens))
            .ToList();
    }

    /// <summary>
    /// Creates a snapshot of the current budget state.
    /// </summary>
    public ContextBudgetSnapshot CreateSnapshot()
    {
        return new ContextBudgetSnapshot
        {
            TotalBudget = _totalBudget,
            AllocatedTokens = _sourceBudgets.Values.Sum(b => b.AllocatedTokens),
            UsedTokens = _sourceBudgets.Values.Sum(b => b.UsedTokens),
            SourceAllocations = _sourceBudgets.ToDictionary(
                kvp => kvp.Key,
                kvp => new SourceAllocationSnapshot
                {
                    Priority = kvp.Value.Priority,
                    Allocated = kvp.Value.AllocatedTokens,
                    Used = kvp.Value.UsedTokens
                },
                StringComparer.Ordinal)
        };
    }

    private sealed class SourceBudget
    {
        public required string SourceId { get; init; }
        public int Priority { get; init; }
        public int MinTokens { get; init; }
        public int MaxTokens { get; init; }
        public int AllocatedTokens { get; set; }
        public int UsedTokens { get; set; }
    }
}

/// <summary>
/// Status of a single source's budget.
/// </summary>
public sealed record BudgetStatus(
    string SourceId,
    int Priority,
    int AllocatedTokens,
    int UsedTokens,
    int RemainingTokens);

/// <summary>
/// Snapshot of the entire context budget state.
/// </summary>
public sealed record ContextBudgetSnapshot
{
    public int TotalBudget { get; init; }
    public int AllocatedTokens { get; init; }
    public int UsedTokens { get; init; }
    public int RemainingBudget => TotalBudget - UsedTokens;
    public required IReadOnlyDictionary<string, SourceAllocationSnapshot> SourceAllocations { get; init; }
}

/// <summary>
/// Snapshot of a single source's allocation.
/// </summary>
public sealed record SourceAllocationSnapshot
{
    public int Priority { get; init; }
    public int Allocated { get; init; }
    public int Used { get; init; }
    public int Remaining => Allocated - Used;
}
