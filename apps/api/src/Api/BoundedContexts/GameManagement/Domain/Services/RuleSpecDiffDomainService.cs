using Api.Models;

namespace Api.BoundedContexts.GameManagement.Domain.Services;

/// <summary>
/// Domain service for computing differences between RuleSpec versions.
/// Pure domain logic with no infrastructure dependencies.
/// </summary>
public class RuleSpecDiffDomainService
{
    /// <summary>
    /// Computes the difference between two RuleSpec versions
    /// </summary>
    public RuleSpecDiff ComputeDiff(RuleSpec from, RuleSpec to)
    {
        var changes = new List<RuleAtomChange>();

        // Create dictionaries for faster lookup
        var fromAtoms = from.rules.ToDictionary(r => r.id, StringComparer.Ordinal);
        var toAtoms = to.rules.ToDictionary(r => r.id, StringComparer.Ordinal);

        var allAtomIds = fromAtoms.Keys.Union(toAtoms.Keys, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

        int added = 0, modified = 0, deleted = 0, unchanged = 0;

        foreach (var atomId in allAtomIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            var existsInFrom = fromAtoms.TryGetValue(atomId, out var fromAtom);
            var existsInTo = toAtoms.TryGetValue(atomId, out var toAtom);

            if (!existsInFrom && existsInTo)
            {
                // Added
                changes.Add(new RuleAtomChange(
                    ChangeType.Added,
                    null,
                    atomId,
                    null,
                    toAtom
                ));
                added++;
            }
            else if (existsInFrom && !existsInTo)
            {
                // Deleted
                changes.Add(new RuleAtomChange(
                    ChangeType.Deleted,
                    atomId,
                    null,
                    fromAtom,
                    null
                ));
                deleted++;
            }
            else if (existsInFrom && existsInTo)
            {
                // Check if modified
                var fieldChanges = CompareAtoms(fromAtom!, toAtom!);

                if (fieldChanges.Count > 0)
                {
                    changes.Add(new RuleAtomChange(
                        ChangeType.Modified,
                        atomId,
                        atomId,
                        fromAtom,
                        toAtom,
                        fieldChanges
                    ));
                    modified++;
                }
                else
                {
                    changes.Add(new RuleAtomChange(
                        ChangeType.Unchanged,
                        atomId,
                        atomId,
                        fromAtom,
                        toAtom
                    ));
                    unchanged++;
                }
            }
        }

        var summary = new DiffSummary(
            TotalChanges: added + modified + deleted,
            Added: added,
            Modified: modified,
            Deleted: deleted,
            Unchanged: unchanged
        );

        return new RuleSpecDiff(
            from.gameId,
            from.version,
            to.version,
            from.createdAt,
            to.createdAt,
            summary,
            changes
        );
    }

    /// <summary>
    /// Generates a human-readable summary of the diff
    /// </summary>
    public string GenerateDiffSummary(RuleSpecDiff diff)
    {
        var lines = new List<string>();

        lines.Add($"Difference between {diff.FromVersion} and {diff.ToVersion}");
        lines.Add($"From: {diff.FromCreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        lines.Add($"To: {diff.ToCreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        lines.Add("");
        lines.Add($"Summary:");
        lines.Add($"  Total changes: {diff.Summary.TotalChanges}");
        lines.Add($"  Added: {diff.Summary.Added}");
        lines.Add($"  Modified: {diff.Summary.Modified}");
        lines.Add($"  Deleted: {diff.Summary.Deleted}");
        lines.Add($"  Unchanged: {diff.Summary.Unchanged}");

        if (diff.Summary.TotalChanges > 0)
        {
            lines.Add("");
            lines.Add("Changes:");

            foreach (var change in diff.Changes.Where(c => c.Type != ChangeType.Unchanged))
            {
                lines.Add("");

                switch (change.Type)
                {
                    case ChangeType.Added:
                        lines.Add($"  + Added: {change.NewAtom}");
                        lines.Add($"    Text: {change.NewValue?.text}");
                        lines.Add($"    Section: {change.NewValue?.section ?? "(none)"}");
                        break;

                    case ChangeType.Deleted:
                        lines.Add($"  - Deleted: {change.OldAtom}");
                        lines.Add($"    Text: {change.OldValue?.text}");
                        lines.Add($"    Section: {change.OldValue?.section ?? "(none)"}");
                        break;

                    case ChangeType.Modified:
                        lines.Add($"  ~ Modified: {change.OldAtom}");
                        if (change.FieldChanges != null)
                        {
                            lines.AddRange(change.FieldChanges.SelectMany(fc => new[]
                            {
                                $"    {fc.FieldName}:",
                                $"      - {fc.OldValue ?? "(null)"}",
                                $"      + {fc.NewValue ?? "(null)"}"
                            }));
                        }
                        break;
                }
            }
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Compares two rule atoms and returns a list of field changes
    /// </summary>
    private List<FieldChange> CompareAtoms(RuleAtom from, RuleAtom to)
    {
        var changes = new List<FieldChange>();

        if (!string.Equals(from.text, to.text, StringComparison.Ordinal))
        {
            changes.Add(new FieldChange("text", from.text, to.text));
        }

        if (!string.Equals(from.section, to.section, StringComparison.Ordinal))
        {
            changes.Add(new FieldChange("section", from.section, to.section));
        }

        if (!string.Equals(from.page, to.page, StringComparison.Ordinal))
        {
            changes.Add(new FieldChange("page", from.page, to.page));
        }

        if (!string.Equals(from.line, to.line, StringComparison.Ordinal))
        {
            changes.Add(new FieldChange("line", from.line, to.line));
        }

        return changes;
    }
}
