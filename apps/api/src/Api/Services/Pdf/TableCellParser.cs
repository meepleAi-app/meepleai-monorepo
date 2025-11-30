using System.Text;
using System.Text.RegularExpressions;

namespace Api.Services.Pdf;

/// <summary>
/// Service responsible for parsing individual table cells and columns
/// </summary>
public class TableCellParser : ITableCellParser
{
#pragma warning disable MA0051 // Method is too long
    public ColumnSplitResult SplitIntoColumns(PositionedTextLine line, List<ColumnBoundary>? existingBoundaries)
    {
        var result = new ColumnSplitResult();

        if (line.Characters.Count == 0)
        {
            if (existingBoundaries != null)
            {
                result.Boundaries = existingBoundaries;
                result.Columns = Enumerable.Repeat(string.Empty, existingBoundaries.Count).ToList();
            }

            return result;
        }

        if (existingBoundaries == null || existingBoundaries.Count == 0)
        {
            var layout = DetectColumnLayout(line);
            result.Boundaries = layout.Boundaries;
            result.Columns = layout.Columns;

            if (result.Columns.Count > 0)
            {
                var text = line.GetTrimmedText();
                var textualColumns = Regex
                    .Split(text, "\\s{2,}", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)
                    .Select(segment => segment.Trim())
                    .ToList();

                if (textualColumns.Count == result.Columns.Count && textualColumns.Count > 0)
                {
                    result.Columns = textualColumns;
                }
            }

            return result;
        }

        var boundaries = existingBoundaries
            .Select(b => b.Clone())
            .OrderBy(b => b.Start)
            .ToList();

        var tolerance = Math.Max(2f, line.GetAverageCharacterWidth() * 0.75f);
        var columnTexts = boundaries.Select(_ => new StringBuilder()).ToList();

        foreach (var character in line.Characters)
        {
            var columnIndex = FindBoundaryIndex(boundaries, character, tolerance);

            if (columnIndex == -1)
            {
                var newBoundary = ColumnBoundary.FromCharacter(character, tolerance);

                var insertIndex = boundaries.FindIndex(b => newBoundary.Start < b.Start);
                if (insertIndex < 0)
                {
                    boundaries.Add(newBoundary);
                    columnTexts.Add(new StringBuilder());
                    columnIndex = boundaries.Count - 1;
                }
                else
                {
                    boundaries.Insert(insertIndex, newBoundary);
                    columnTexts.Insert(insertIndex, new StringBuilder());
                    columnIndex = insertIndex;
                }
            }

            var boundary = boundaries[columnIndex];
            boundary.Start = Math.Min(boundary.Start, character.X - tolerance);
            boundary.End = Math.Max(boundary.End, character.EndX + tolerance);
            columnTexts[columnIndex].Append(character.Text);
        }

        result.Boundaries = boundaries;
        result.Columns = columnTexts.Select(sb => sb.ToString().Trim()).ToList();

        return result;
    }
#pragma warning restore MA0051 // Method is too long

    public DetectedColumnLayout DetectColumnLayout(PositionedTextLine line)
    {
        var layout = new DetectedColumnLayout();

        if (line.Characters.Count == 0)
        {
            return layout;
        }

        var threshold = CalculateGapThreshold(line);
        var overlapTolerance = CalculateOverlapTolerance(line);

        ColumnBoundary? currentBoundary = null;
        StringBuilder? currentText = null;

        foreach (var character in line.Characters)
        {
            if (currentBoundary == null)
            {
                currentBoundary = ColumnBoundary.FromCharacter(character);
                currentText = new StringBuilder(character.Text);
                continue;
            }

            var gap = character.X - currentBoundary.End;

            if (gap > threshold || gap < -overlapTolerance)
            {
                CommitCurrent();
                currentBoundary = ColumnBoundary.FromCharacter(character);
                currentText = new StringBuilder(character.Text);
            }
            else
            {
                currentBoundary.ExpandToInclude(character);
                // currentText should always be non-null here since it's set with currentBoundary
                // but we add a defensive check to satisfy the compiler
                if (currentText != null)
                {
                    currentText.Append(character.Text);
                }
            }
        }

        CommitCurrent();

        ApplyPadding(layout.Boundaries, Math.Max(2f, threshold / 3f));
        EnsureNonOverlappingBoundaries(layout.Boundaries);

        layout.Columns = layout.Columns
            .Select(text => text.Trim())
            .ToList();

        return layout;

        void CommitCurrent()
        {
            if (currentBoundary != null && currentText != null)
            {
                layout.Boundaries.Add(currentBoundary);
                layout.Columns.Add(currentText.ToString());
            }

            currentBoundary = null;
            currentText = null;
        }
    }

    public int FindBoundaryIndex(List<ColumnBoundary> boundaries, PositionedCharacter character, float tolerance)
    {
        var center = character.CenterX;

        var candidateIndex = -1;
        var candidateScore = float.MaxValue;

        for (int i = 0; i < boundaries.Count; i++)
        {
            var boundary = boundaries[i];
            if (center >= boundary.Start - tolerance && center <= boundary.End + tolerance)
            {
                var boundaryCenter = boundary.Center;
                var distance = Math.Abs(center - boundaryCenter);

                if (center >= boundary.Start && center <= boundary.End)
                {
                    distance *= 0.5f;
                }

                if (distance < candidateScore)
                {
                    candidateScore = distance;
                    candidateIndex = i;
                }
            }
        }

        if (candidateIndex != -1)
        {
            return candidateIndex;
        }

        var closestIndex = -1;
        var closestDistance = float.MaxValue;

        for (int i = 0; i < boundaries.Count; i++)
        {
            var boundary = boundaries[i];
            var distance = Math.Min(Math.Abs(center - boundary.Start), Math.Abs(center - boundary.End));

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        if (closestDistance <= tolerance * 2)
        {
            return closestIndex;
        }

        return -1;
    }

    public float CalculateOverlapTolerance(PositionedTextLine line)
    {
        var averageWidth = line.GetAverageCharacterWidth();

        if (averageWidth <= 0)
        {
            return 1.5f;
        }

        return Math.Max(1.5f, averageWidth * 0.6f);
    }

    public float CalculateGapThreshold(PositionedTextLine line)
    {
        if (line.Characters.Count < 2)
        {
            return Math.Max(6f, line.GetAverageCharacterWidth() * 2.5f);
        }

        var gaps = new List<float>();

        for (int i = 1; i < line.Characters.Count; i++)
        {
            var previous = line.Characters[i - 1];
            var current = line.Characters[i];
            var gap = current.X - previous.EndX;

            if (gap > 0)
            {
                gaps.Add(gap);
            }
        }

        var averageWidth = line.GetAverageCharacterWidth();
        var baseline = Math.Max(6f, averageWidth > 0 ? averageWidth * 2f : 6f);

        if (gaps.Count == 0)
        {
            return baseline;
        }

        gaps.Sort();

        var smallGapThreshold = baseline * 1.75f;
        var smallGaps = gaps.Where(gap => gap <= smallGapThreshold).ToList();

        float threshold;

        if (smallGaps.Count >= 2)
        {
            var typicalGap = smallGaps.Average();
            threshold = typicalGap * 1.5f;
        }
        else if (smallGaps.Count == 1 && gaps.Count > 1)
        {
            var median = gaps[gaps.Count / 2];
            threshold = median * 0.9f;
        }
        else
        {
            var candidate = gaps.Min() * 0.9f;
            threshold = candidate;
        }

        var upperBound = baseline * 4f;
        threshold = Math.Min(threshold, upperBound);
        threshold = Math.Max(threshold, baseline);

        return threshold;
    }

    private static void ApplyPadding(List<ColumnBoundary> boundaries, float padding)
    {
        if (padding <= 0f)
        {
            return;
        }

        foreach (var boundary in boundaries)
        {
            boundary.Start -= padding;
            boundary.End += padding;
        }
    }

    private static void EnsureNonOverlappingBoundaries(List<ColumnBoundary> boundaries)
    {
        if (boundaries.Count < 2)
        {
            return;
        }

        boundaries.Sort((a, b) => a.Start.CompareTo(b.Start));

        for (int i = 1; i < boundaries.Count; i++)
        {
            var previous = boundaries[i - 1];
            var current = boundaries[i];

            if (previous.End > current.Start)
            {
                var midpoint = (previous.End + current.Start) / 2f;
                previous.End = midpoint;
                current.Start = midpoint;
            }

            if (current.Start > current.End)
            {
                current.Start = current.End;
            }

            if (previous.Start > previous.End)
            {
                previous.Start = previous.End;
            }
        }
    }
}
