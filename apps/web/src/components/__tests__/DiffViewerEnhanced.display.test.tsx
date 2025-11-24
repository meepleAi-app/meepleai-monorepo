import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DiffViewerEnhanced } from '../DiffViewerEnhanced';

// Mock child components
vi.mock('../DiffSummary', () => ({
  DiffSummary: ({ summary }: any) => (
    <div data-testid="diff-summary">
      {summary.added} added, {summary.modified} modified, {summary.deleted} deleted
    </div>
  ),
}));

vi.mock('../ChangeItem', () => ({
  ChangeItem: ({ change }: any) => (
    <div data-testid="change-item" data-type={change.type}>
      {change.type}
    </div>
  ),
}));

vi.mock('../diff/DiffViewModeToggle', () => ({
  DiffViewModeToggle: ({ currentMode, onModeChange }: any) => (
    <div data-testid="view-mode-toggle">
      <button onClick={() => onModeChange('list')} data-testid="list-mode-button">
        List
      </button>
      <button onClick={() => onModeChange('side-by-side')} data-testid="side-by-side-mode-button">
        Side-by-Side
      </button>
      <span data-testid="current-mode">{currentMode}</span>
    </div>
  ),
}));

vi.mock('../diff/DiffToolbar', () => ({
  DiffToolbar: ({ searchQuery, onSearchChange, onNavigatePrev, onNavigateNext }: any) => (
    <div data-testid="diff-toolbar">
      <input
        data-testid="search-input"
        value={searchQuery}
        onChange={(e) => onSearchChange(e.target.value)}
      />
      <button onClick={onNavigatePrev} data-testid="prev-button">
        Prev
      </button>
      <button onClick={onNavigateNext} data-testid="next-button">
        Next
      </button>
    </div>
  ),
}));

vi.mock('../diff/SideBySideDiffView', () => ({
  SideBySideDiffView: () => (
    <div data-testid="side-by-side-view">Side-by-Side View</div>
  ),
}));

// Mock diffProcessor
vi.mock('../../lib/diffProcessor', () => ({
  processDiff: vi.fn((oldJson: string, newJson: string) => ({
    oldLines: [
      { lineNumber: 1, content: 'old line 1', type: 'deleted' },
      { lineNumber: 2, content: 'old line 2', type: 'unchanged' },
    ],
    newLines: [
      { lineNumber: 1, content: 'new line 1', type: 'added' },
      { lineNumber: 2, content: 'new line 2', type: 'unchanged' },
    ],
    changes: [
      { id: 'change-1', startLine: 1, endLine: 1, type: 'added', oldStartLine: 0, oldEndLine: 0, newStartLine: 1, newEndLine: 1 },
    ],
    statistics: {
      added: 1,
      deleted: 1,
      modified: 0,
      unchanged: 1,
      totalLines: 3,
    },
  })),
  identifyCollapsibleSections: vi.fn(() => []),
  filterChangesByQuery: vi.fn((changes) => changes),
}));

describe('DiffViewerEnhanced', () => {
  const mockDiff = {
    gameId: 'test-game',
    fromVersion: '1.0.0',
    toVersion: '1.1.0',
    fromCreatedAt: '2025-01-01T00:00:00Z',
    toCreatedAt: '2025-01-15T00:00:00Z',
    summary: {
      totalChanges: 3,
      added: 1,
      modified: 1,
      deleted: 1,
      unchanged: 10,
    },
    changes: [
      {
        type: 'Added' as const,
        oldAtom: null,
        newAtom: 'new-atom-1',
        oldValue: null,
        newValue: {
          id: 'rule-1',
          text: 'New rule added',
          section: 'Setup',
          page: '1',
          line: '5',
        },
        fieldChanges: null,
      },
      {
        type: 'Modified' as const,
        oldAtom: 'old-atom-2',
        newAtom: 'new-atom-2',
        oldValue: {
          id: 'rule-2',
          text: 'Old rule text',
          section: 'Gameplay',
          page: '2',
          line: '10',
        },
        newValue: {
          id: 'rule-2',
          text: 'Modified rule text',
          section: 'Gameplay',
          page: '2',
          line: '10',
        },
        fieldChanges: [
          {
            fieldName: 'text',
            oldValue: 'Old rule text',
            newValue: 'Modified rule text',
          },
        ],
      },
      {
        type: 'Deleted' as const,
        oldAtom: 'old-atom-3',
        newAtom: null,
        oldValue: {
          id: 'rule-3',
          text: 'Deleted rule',
          section: 'End',
          page: '3',
          line: '15',
        },
        newValue: null,
        fieldChanges: null,
      },
      {
        type: 'Unchanged' as const,
        oldAtom: 'atom-4',
        newAtom: 'atom-4',
        oldValue: {
          id: 'rule-4',
          text: 'Unchanged rule',
          section: 'Scoring',
          page: '4',
          line: '20',
        },
        newValue: {
          id: 'rule-4',
          text: 'Unchanged rule',
          section: 'Scoring',
          page: '4',
          line: '20',
        },
        fieldChanges: null,
      },
    ],
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('List View Mode', () => {
    it('should render in list view by default', () => {
      render(<DiffViewerEnhanced diff={mockDiff} showOnlyChanges={false} />);

      expect(screen.getByTestId('current-mode')).toHaveTextContent('list');
    });

    it('should display diff summary', () => {
      render(<DiffViewerEnhanced diff={mockDiff} showOnlyChanges={false} />);

      const summaries = screen.getAllByTestId('diff-summary');
      expect(summaries[0]).toHaveTextContent('1 added, 1 modified, 1 deleted');
    });

    it('should display all changes when showOnlyChanges is false', () => {
      render(<DiffViewerEnhanced diff={mockDiff} showOnlyChanges={false} />);

      const changeItems = screen.getAllByTestId('change-item');
      expect(changeItems).toHaveLength(4); // All changes including Unchanged
    });

    it('should display only non-unchanged changes when showOnlyChanges is true', () => {
      render(<DiffViewerEnhanced diff={mockDiff} showOnlyChanges={true} />);

      const changeItems = screen.getAllByTestId('change-item');
      expect(changeItems).toHaveLength(3); // Exclude Unchanged
    });

    it('should show change count', () => {
      render(<DiffViewerEnhanced diff={mockDiff} showOnlyChanges={false} />);

      expect(screen.getByText(/Modifiche \(4\)/)).toBeInTheDocument();
    });

    it('should show "no changes" message when no changes to display', () => {
      const emptyDiff = {
        ...mockDiff,
        changes: [],
      };

      render(<DiffViewerEnhanced diff={emptyDiff} showOnlyChanges={false} />);

      expect(screen.getByText('Nessuna modifica da visualizzare')).toBeInTheDocument();
    });

    it('should render view mode toggle', () => {
      render(<DiffViewerEnhanced diff={mockDiff} showOnlyChanges={false} />);

      expect(screen.getByTestId('view-mode-toggle')).toBeInTheDocument();
    });

    it('should have hidden test data elements for compatibility', () => {
      const { container } = render(<DiffViewerEnhanced diff={mockDiff} showOnlyChanges={false} />);

      const fromVersion = container.querySelector('[data-testid="diff-from-version"]');
      const toVersion = container.querySelector('[data-testid="diff-to-version"]');
      const showOnlyChangesEl = container.querySelector('[data-testid="diff-show-only-changes"]');

      expect(fromVersion).toHaveTextContent('1.0.0');
      expect(toVersion).toHaveTextContent('1.1.0');
      expect(showOnlyChangesEl).toHaveTextContent('false');
    });
  });

  describe('Side-by-Side View Mode', () => {
    it('should switch to side-by-side view', async () => {
      const user = userEvent.setup();

      render(<DiffViewerEnhanced diff={mockDiff} showOnlyChanges={false} />);

      const sideBySideButton = screen.getByTestId('side-by-side-mode-button');
      await user.click(sideBySideButton);

      await waitFor(() => {
        expect(screen.getByTestId('side-by-side-view')).toBeInTheDocument();
      });
    });

    it('should render toolbar in side-by-side mode', async () => {
      const user = userEvent.setup();

      render(<DiffViewerEnhanced diff={mockDiff} showOnlyChanges={false} />);

      const sideBySideButton = screen.getByTestId('side-by-side-mode-button');
      await user.click(sideBySideButton);

      await waitFor(() => {
        expect(screen.getByTestId('diff-toolbar')).toBeInTheDocument();
      });
    });

    it('should handle search in side-by-side mode', async () => {
      const user = userEvent.setup();

      render(<DiffViewerEnhanced diff={mockDiff} showOnlyChanges={false} />);

      const sideBySideButton = screen.getByTestId('side-by-side-mode-button');
      await user.click(sideBySideButton);

      await waitFor(() => {
        expect(screen.getByTestId('search-input')).toBeInTheDocument();
      });

      const searchInput = screen.getByTestId('search-input');
      await user.type(searchInput, 'search term');

      expect(searchInput).toHaveValue('search term');
    });

    it('should start with defaultViewMode side-by-side', () => {
      render(
        <DiffViewerEnhanced diff={mockDiff} showOnlyChanges={false} defaultViewMode="side-by-side" />
      );

      expect(screen.getByTestId('side-by-side-view')).toBeInTheDocument();
    });

    it('should fallback to list view if processing fails', () => {
