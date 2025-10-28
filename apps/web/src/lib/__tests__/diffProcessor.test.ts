import {
  processDiff,
  calculateStatistics,
  identifyCollapsibleSections,
  filterChangesByQuery,
  DiffLine
} from '../diffProcessor';

describe('diffProcessor', () => {
  describe('processDiff', () => {
    it('should process basic additions', () => {
      const oldContent = 'line1\nline2\nline4';
      const newContent = 'line1\nline2\nline3\nline4';

      const result = processDiff(oldContent, newContent);

      expect(result.newLines).toHaveLength(4);
      expect(result.newLines[2]).toMatchObject({
        type: 'added',
        content: 'line3',
        newLineNumber: 3
      });
    });

    it('should process basic deletions', () => {
      const oldContent = 'line1\nline2\nline3\nline4';
      const newContent = 'line1\nline2\nline4';

      const result = processDiff(oldContent, newContent);

      expect(result.oldLines).toContainEqual(
        expect.objectContaining({
          type: 'deleted',
          content: 'line3'
        })
      );
    });

    it('should handle empty diff', () => {
      const result = processDiff('', '');

      expect(result.oldLines).toHaveLength(0);
      expect(result.newLines).toHaveLength(0);
      expect(result.changes).toHaveLength(0);
    });

    it('should handle identical content', () => {
      const content = 'line1\nline2\nline3';
      const result = processDiff(content, content);

      expect(result.statistics.added).toBe(0);
      expect(result.statistics.deleted).toBe(0);
      expect(result.statistics.unchanged).toBeGreaterThan(0);
    });

    it('should process mixed changes', () => {
      const oldContent = 'line1\nline2\nline3';
      const newContent = 'line1\nlineX\nline3\nline4';

      const result = processDiff(oldContent, newContent);

      expect(result.statistics.added).toBeGreaterThan(0);
      expect(result.statistics.deleted).toBeGreaterThan(0);
      expect(result.statistics.unchanged).toBeGreaterThan(0);
    });
  });

  describe('calculateStatistics', () => {
    it('should calculate statistics correctly', () => {
      const oldLines: DiffLine[] = [
        { lineNumber: 1, content: 'a', type: 'unchanged' },
        { lineNumber: 2, content: 'b', type: 'deleted' },
        { lineNumber: null, content: '', type: 'added' }
      ];

      const newLines: DiffLine[] = [
        { lineNumber: 1, content: 'a', type: 'unchanged' },
        { lineNumber: null, content: '', type: 'deleted' },
        { lineNumber: 2, content: 'c', type: 'added' }
      ];

      const stats = calculateStatistics(oldLines, newLines);

      expect(stats.added).toBe(1);
      expect(stats.deleted).toBe(1);
      expect(stats.unchanged).toBe(1);
    });
  });

  describe('identifyCollapsibleSections', () => {
    it('should identify sections with >5 unchanged lines', () => {
      const lines: DiffLine[] = Array.from({ length: 10 }, (_, i) => ({
        lineNumber: i + 1,
        content: `line${i + 1}`,
        type: 'unchanged' as const
      }));

      const sections = identifyCollapsibleSections(lines, 5);

      expect(sections).toHaveLength(1);
      expect(sections[0].lineCount).toBe(10);
      expect(sections[0].isCollapsed).toBe(true);
    });

    it('should not identify sections below threshold', () => {
      const lines: DiffLine[] = Array.from({ length: 4 }, (_, i) => ({
        lineNumber: i + 1,
        content: `line${i + 1}`,
        type: 'unchanged' as const
      }));

      const sections = identifyCollapsibleSections(lines, 5);

      expect(sections).toHaveLength(0);
    });

    it('should handle mixed changed/unchanged lines', () => {
      const lines: DiffLine[] = [
        ...Array.from({ length: 6 }, (_, i) => ({
          lineNumber: i + 1,
          content: `unchanged${i + 1}`,
          type: 'unchanged' as const
        })),
        { lineNumber: 7, content: 'changed', type: 'added' as const },
        ...Array.from({ length: 6 }, (_, i) => ({
          lineNumber: i + 8,
          content: `unchanged${i + 8}`,
          type: 'unchanged' as const
        }))
      ];

      const sections = identifyCollapsibleSections(lines, 5);

      expect(sections).toHaveLength(2);
    });
  });

  describe('filterChangesByQuery', () => {
    it('should filter changes by query', () => {
      const oldLines: DiffLine[] = [
        { lineNumber: 1, content: 'const foo = 1', type: 'deleted' }
      ];
      const newLines: DiffLine[] = [
        { lineNumber: 1, content: 'const bar = 1', type: 'added' }
      ];
      const changes = [
        {
          id: 'change-1',
          startLine: 1,
          endLine: 1,
          type: 'added' as const,
          oldStartLine: 1,
          oldEndLine: 1,
          newStartLine: 1,
          newEndLine: 1
        }
      ];

      const filtered = filterChangesByQuery(changes, oldLines, newLines, 'bar');

      expect(filtered).toHaveLength(1);
    });

    it('should return all changes for empty query', () => {
      const changes = [
        {
          id: 'change-1',
          startLine: 1,
          endLine: 1,
          type: 'added' as const,
          oldStartLine: 1,
          oldEndLine: 1,
          newStartLine: 1,
          newEndLine: 1
        }
      ];

      const filtered = filterChangesByQuery(changes, [], [], '');

      expect(filtered).toHaveLength(1);
    });

    it('should be case-insensitive', () => {
      const oldLines: DiffLine[] = [];
      const newLines: DiffLine[] = [
        { lineNumber: 1, content: 'const FOO = 1', type: 'added' }
      ];
      const changes = [
        {
          id: 'change-1',
          startLine: 1,
          endLine: 1,
          type: 'added' as const,
          oldStartLine: 1,
          oldEndLine: 1,
          newStartLine: 1,
          newEndLine: 1
        }
      ];

      const filtered = filterChangesByQuery(changes, oldLines, newLines, 'foo');

      expect(filtered).toHaveLength(1);
    });
  });
});
