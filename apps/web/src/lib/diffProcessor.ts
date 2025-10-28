import { diffLines } from 'diff';

// Core data types for processed diff
export interface DiffLine {
  lineNumber: number | null;
  content: string;
  type: 'added' | 'deleted' | 'modified' | 'unchanged';
  originalLineNumber?: number;
  newLineNumber?: number;
  matchingLineId?: string;
}

export interface DiffChangeGroup {
  id: string;
  startLine: number;
  endLine: number;
  type: 'added' | 'deleted' | 'modified';
  oldStartLine: number;
  oldEndLine: number;
  newStartLine: number;
  newEndLine: number;
}

export interface DiffStatistics {
  added: number;
  deleted: number;
  modified: number;
  unchanged: number;
  totalLines: number;
}

export interface CollapsibleSection {
  startLine: number;
  endLine: number;
  lineCount: number;
  isCollapsed: boolean;
}

export interface ProcessedDiff {
  oldLines: DiffLine[];
  newLines: DiffLine[];
  changes: DiffChangeGroup[];
  statistics: DiffStatistics;
}

/**
 * Process diff between old and new JSON objects
 * Converts to line-by-line diff with metadata for rendering
 */
export function processDiff(
  oldContent: string,
  newContent: string
): ProcessedDiff {
  const diff = diffLines(oldContent, newContent);

  const oldLines: DiffLine[] = [];
  const newLines: DiffLine[] = [];
  const changes: DiffChangeGroup[] = [];

  let oldLineNum = 1;
  let newLineNum = 1;
  let changeGroupId = 0;

  diff.forEach((part) => {
    const lines = part.value.split('\n').filter(Boolean);

    if (part.added) {
      // Lines only in new version
      const changeStart = newLineNum;
      lines.forEach((content) => {
        newLines.push({
          lineNumber: newLineNum++,
          content,
          type: 'added',
          newLineNumber: newLineNum - 1
        });
        oldLines.push({
          lineNumber: null,
          content: '',
          type: 'added',
        });
      });

      changes.push({
        id: `change-${changeGroupId++}`,
        startLine: changeStart,
        endLine: newLineNum - 1,
        type: 'added',
        oldStartLine: oldLineNum,
        oldEndLine: oldLineNum,
        newStartLine: changeStart,
        newEndLine: newLineNum - 1
      });
    } else if (part.removed) {
      // Lines only in old version
      const changeStart = oldLineNum;
      lines.forEach((content) => {
        oldLines.push({
          lineNumber: oldLineNum++,
          content,
          type: 'deleted',
          originalLineNumber: oldLineNum - 1
        });
        newLines.push({
          lineNumber: null,
          content: '',
          type: 'deleted',
        });
      });

      changes.push({
        id: `change-${changeGroupId++}`,
        startLine: changeStart,
        endLine: oldLineNum - 1,
        type: 'deleted',
        oldStartLine: changeStart,
        oldEndLine: oldLineNum - 1,
        newStartLine: newLineNum,
        newEndLine: newLineNum
      });
    } else {
      // Unchanged lines in both versions
      lines.forEach((content) => {
        const matchingId = `line-${oldLineNum}-${newLineNum}`;
        oldLines.push({
          lineNumber: oldLineNum++,
          content,
          type: 'unchanged',
          originalLineNumber: oldLineNum - 1,
          matchingLineId: matchingId
        });
        newLines.push({
          lineNumber: newLineNum++,
          content,
          type: 'unchanged',
          newLineNumber: newLineNum - 1,
          matchingLineId: matchingId
        });
      });
    }
  });

  const statistics = calculateStatistics(oldLines, newLines);

  return {
    oldLines,
    newLines,
    changes,
    statistics
  };
}

/**
 * Calculate diff statistics from processed lines
 */
export function calculateStatistics(
  oldLines: DiffLine[],
  newLines: DiffLine[]
): DiffStatistics {
  const added = newLines.filter(l => l.type === 'added').length;
  const deleted = oldLines.filter(l => l.type === 'deleted').length;
  const unchanged = oldLines.filter(l => l.type === 'unchanged').length;

  return {
    added,
    deleted,
    modified: 0, // We'll calculate this from field-level changes later
    unchanged,
    totalLines: Math.max(oldLines.length, newLines.length)
  };
}

/**
 * Identify sections with consecutive unchanged lines (>5 lines)
 * These can be collapsed to reduce visual clutter
 */
export function identifyCollapsibleSections(
  lines: DiffLine[],
  minConsecutiveLines: number = 5
): CollapsibleSection[] {
  const sections: CollapsibleSection[] = [];
  let currentSection: CollapsibleSection | null = null as CollapsibleSection | null;

  lines.forEach((line, index) => {
    if (line.type === 'unchanged' && line.lineNumber !== null) {
      if (!currentSection) {
        currentSection = {
          startLine: line.lineNumber,
          endLine: line.lineNumber,
          lineCount: 1,
          isCollapsed: true // Default to collapsed
        };
      } else {
        currentSection.endLine = line.lineNumber;
        currentSection.lineCount++;
      }
    } else {
      // End current section on non-unchanged line
      if (currentSection && currentSection.lineCount >= minConsecutiveLines) {
        sections.push(currentSection);
      }
      currentSection = null;
    }
  });

  // Handle final section
  if (currentSection && currentSection.lineCount >= minConsecutiveLines) {
    sections.push(currentSection);
  }

  return sections;
}

/**
 * Filter change groups by search query
 */
export function filterChangesByQuery(
  changes: DiffChangeGroup[],
  oldLines: DiffLine[],
  newLines: DiffLine[],
  query: string
): DiffChangeGroup[] {
  if (!query.trim()) return changes;

  const lowerQuery = query.toLowerCase();

  return changes.filter((change) => {
    const relevantOldLines = oldLines.slice(
      change.oldStartLine - 1,
      change.oldEndLine
    );
    const relevantNewLines = newLines.slice(
      change.newStartLine - 1,
      change.newEndLine
    );

    const allLines = [...relevantOldLines, ...relevantNewLines];
    return allLines.some(line =>
      line.content.toLowerCase().includes(lowerQuery)
    );
  });
}
