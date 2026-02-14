/**
 * BulkImportResults Component & CSV Export Tests - Issue #4175
 *
 * Test coverage:
 * - generateCSV function (unit tests)
 * - escapeCSVField function (via generateCSV)
 * - BulkImportResults component rendering
 * - Stats summary display (total, enqueued, skipped, failed)
 * - Errors table display
 * - CSV download button
 * - New import button
 * - Full success vs partial success states
 *
 * Target: >= 85% coverage
 */

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  BulkImportResults,
  generateCSV,
} from '@/app/(authenticated)/admin/games/import/bulk/BulkImportResults';
import type { BulkImportFromJsonResult } from '@/lib/api/schemas/admin.schemas';

// ─── Test Data ──────────────────────────────────────────────────

const fullSuccessResult: BulkImportFromJsonResult = {
  total: 5,
  enqueued: 5,
  skipped: 0,
  failed: 0,
  errors: [],
};

const partialSuccessResult: BulkImportFromJsonResult = {
  total: 10,
  enqueued: 6,
  skipped: 2,
  failed: 2,
  errors: [
    { bggId: 174430, gameName: 'Gloomhaven', reason: 'Already exists in catalog', errorType: 'Duplicate' },
    { bggId: 167791, gameName: 'Terraforming Mars', reason: 'BGG API timeout', errorType: 'ApiError' },
    { bggId: null, gameName: null, reason: 'Invalid BGG ID format', errorType: 'ValidationError' },
    { bggId: 169786, gameName: 'Scythe', reason: 'Rate limited', errorType: 'RateLimit' },
  ],
};

const allFailedResult: BulkImportFromJsonResult = {
  total: 3,
  enqueued: 0,
  skipped: 0,
  failed: 3,
  errors: [
    { bggId: 1, gameName: 'Game 1', reason: 'Error 1', errorType: 'ApiError' },
    { bggId: 2, gameName: 'Game 2', reason: 'Error 2', errorType: 'ApiError' },
    { bggId: 3, gameName: 'Game 3', reason: 'Error 3', errorType: 'ApiError' },
  ],
};

// ─── generateCSV Tests ──────────────────────────────────────────

describe('generateCSV', () => {
  it('generates summary section with correct counts', () => {
    const csv = generateCSV(fullSuccessResult);
    const lines = csv.split('\n');

    expect(lines[0]).toBe('Bulk Import Report');
    expect(lines[1]).toBe('Total,5');
    expect(lines[2]).toBe('Enqueued,5');
    expect(lines[3]).toBe('Skipped,0');
    expect(lines[4]).toBe('Failed,0');
  });

  it('includes error detail rows when errors exist', () => {
    const csv = generateCSV(partialSuccessResult);
    const lines = csv.split('\n');

    // Summary (5 lines) + blank line + header + 4 error rows = 11 lines
    expect(lines[6]).toBe('BGG ID,Game Name,Error Type,Reason');
    expect(lines[7]).toContain('174430');
    expect(lines[7]).toContain('Gloomhaven');
    expect(lines[7]).toContain('Duplicate');
    expect(lines[7]).toContain('Already exists in catalog');
  });

  it('does not include error section when no errors', () => {
    const csv = generateCSV(fullSuccessResult);
    const lines = csv.split('\n');

    expect(lines).not.toContain('BGG ID,Game Name,Error Type,Reason');
  });

  it('handles null bggId and gameName in errors', () => {
    const csv = generateCSV(partialSuccessResult);
    const lines = csv.split('\n');

    // The third error has null bggId and gameName
    const nullErrorLine = lines[9];
    expect(nullErrorLine).toContain('ValidationError');
    expect(nullErrorLine).toContain('Invalid BGG ID format');
  });

  it('escapes CSV fields with commas', () => {
    const result: BulkImportFromJsonResult = {
      total: 1,
      enqueued: 0,
      skipped: 0,
      failed: 1,
      errors: [
        { bggId: 1, gameName: 'Game, With Comma', reason: 'Some, reason', errorType: 'Error' },
      ],
    };
    const csv = generateCSV(result);

    expect(csv).toContain('"Game, With Comma"');
    expect(csv).toContain('"Some, reason"');
  });

  it('escapes CSV fields with double quotes', () => {
    const result: BulkImportFromJsonResult = {
      total: 1,
      enqueued: 0,
      skipped: 0,
      failed: 1,
      errors: [
        { bggId: 1, gameName: 'Game "Quoted"', reason: 'Has "quotes"', errorType: 'Error' },
      ],
    };
    const csv = generateCSV(result);

    expect(csv).toContain('"Game ""Quoted"""');
    expect(csv).toContain('"Has ""quotes"""');
  });

  it('escapes CSV fields with newlines', () => {
    const result: BulkImportFromJsonResult = {
      total: 1,
      enqueued: 0,
      skipped: 0,
      failed: 1,
      errors: [
        { bggId: 1, gameName: 'Game', reason: 'Line 1\nLine 2', errorType: 'Error' },
      ],
    };
    const csv = generateCSV(result);

    expect(csv).toContain('"Line 1\nLine 2"');
  });
});

// ─── BulkImportResults Component Tests ──────────────────────────

describe('BulkImportResults', () => {
  const mockOnNewImport = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders the results card', () => {
      render(<BulkImportResults result={fullSuccessResult} />);
      expect(screen.getByTestId('bulk-import-results')).toBeInTheDocument();
    });

    it('renders "Import Complete" title', () => {
      render(<BulkImportResults result={fullSuccessResult} />);
      expect(screen.getByText('Import Complete')).toBeInTheDocument();
    });

    it('applies custom className', () => {
      render(<BulkImportResults result={fullSuccessResult} className="custom-class" />);
      const card = screen.getByTestId('bulk-import-results');
      expect(card.className).toContain('custom-class');
    });
  });

  describe('Success State', () => {
    it('shows green check icon for full success', () => {
      const { container } = render(<BulkImportResults result={fullSuccessResult} />);
      const checkIcon = container.querySelector('.text-green-500');
      expect(checkIcon).toBeInTheDocument();
    });

    it('does not show errors section for full success', () => {
      render(<BulkImportResults result={fullSuccessResult} />);
      expect(screen.queryByTestId('errors-section')).not.toBeInTheDocument();
    });
  });

  describe('Partial Success State', () => {
    it('shows amber warning icon for partial success', () => {
      const { container } = render(<BulkImportResults result={partialSuccessResult} />);
      const warningIcon = container.querySelector('.text-amber-500');
      expect(warningIcon).toBeInTheDocument();
    });

    it('shows errors section when errors exist', () => {
      render(<BulkImportResults result={partialSuccessResult} />);
      expect(screen.getByTestId('errors-section')).toBeInTheDocument();
    });
  });

  describe('Stats Summary', () => {
    it('displays correct stat values', () => {
      render(<BulkImportResults result={partialSuccessResult} />);

      const statsGrid = screen.getByTestId('results-stats');
      expect(within(statsGrid).getByTestId('result-stat-total')).toHaveTextContent('10');
      expect(within(statsGrid).getByTestId('result-stat-enqueued')).toHaveTextContent('6');
      expect(within(statsGrid).getByTestId('result-stat-skipped')).toHaveTextContent('2');
      expect(within(statsGrid).getByTestId('result-stat-failed')).toHaveTextContent('2');
    });

    it('displays stat labels', () => {
      render(<BulkImportResults result={fullSuccessResult} />);

      expect(screen.getByText('Total')).toBeInTheDocument();
      expect(screen.getByText('Enqueued')).toBeInTheDocument();
      expect(screen.getByText('Skipped')).toBeInTheDocument();
      expect(screen.getByText('Failed')).toBeInTheDocument();
    });

    it('shows zero values correctly', () => {
      render(<BulkImportResults result={fullSuccessResult} />);

      expect(screen.getByTestId('result-stat-skipped')).toHaveTextContent('0');
      expect(screen.getByTestId('result-stat-failed')).toHaveTextContent('0');
    });
  });

  describe('Errors Table', () => {
    it('renders error rows with correct data', () => {
      render(<BulkImportResults result={partialSuccessResult} />);

      expect(screen.getByText('Gloomhaven')).toBeInTheDocument();
      expect(screen.getByText('Terraforming Mars')).toBeInTheDocument();
      expect(screen.getByText('Already exists in catalog')).toBeInTheDocument();
      expect(screen.getByText('BGG API timeout')).toBeInTheDocument();
    });

    it('renders error type badges', () => {
      render(<BulkImportResults result={partialSuccessResult} />);

      expect(screen.getByText('Duplicate')).toBeInTheDocument();
      expect(screen.getByText('ApiError')).toBeInTheDocument();
      expect(screen.getByText('ValidationError')).toBeInTheDocument();
    });

    it('uses secondary variant for Duplicate badge', () => {
      render(<BulkImportResults result={partialSuccessResult} />);

      const duplicateBadge = screen.getByText('Duplicate');
      expect(duplicateBadge.className).not.toContain('destructive');
    });

    it('uses destructive variant for non-Duplicate badges', () => {
      render(<BulkImportResults result={partialSuccessResult} />);

      const apiErrorBadge = screen.getByText('ApiError');
      expect(apiErrorBadge.className).toContain('destructive');
    });

    it('shows dash for null bggId', () => {
      render(<BulkImportResults result={partialSuccessResult} />);

      const errorsSection = screen.getByTestId('errors-section');
      const rows = errorsSection.querySelectorAll('tr');
      // Find the row with ValidationError (null bggId)
      const validationRow = Array.from(rows).find(
        (row) => row.textContent?.includes('ValidationError')
      );
      expect(validationRow?.textContent).toContain('-');
    });

    it('shows dash for null gameName', () => {
      render(<BulkImportResults result={partialSuccessResult} />);

      const errorsSection = screen.getByTestId('errors-section');
      const rows = errorsSection.querySelectorAll('tr');
      const validationRow = Array.from(rows).find(
        (row) => row.textContent?.includes('ValidationError')
      );
      expect(validationRow?.textContent).toContain('-');
    });

    it('renders table headers', () => {
      render(<BulkImportResults result={partialSuccessResult} />);

      expect(screen.getByText('BGG ID')).toBeInTheDocument();
      expect(screen.getByText('Game')).toBeInTheDocument();
      expect(screen.getByText('Type')).toBeInTheDocument();
      expect(screen.getByText('Reason')).toBeInTheDocument();
    });
  });

  describe('Action Buttons', () => {
    it('renders download CSV button', () => {
      render(<BulkImportResults result={fullSuccessResult} />);

      expect(screen.getByTestId('download-csv-button')).toBeInTheDocument();
      expect(screen.getByText('Download CSV Report')).toBeInTheDocument();
    });

    it('triggers CSV download on click', async () => {
      const user = userEvent.setup();
      const createObjectURL = vi.fn(() => 'blob:test');
      const revokeObjectURL = vi.fn();
      const mockClick = vi.fn();

      global.URL.createObjectURL = createObjectURL;
      global.URL.revokeObjectURL = revokeObjectURL;

      const originalCreateElement = document.createElement.bind(document);
      vi.spyOn(document, 'createElement').mockImplementation((tag: string) => {
        if (tag === 'a') {
          const a = originalCreateElement('a');
          a.click = mockClick;
          return a;
        }
        return originalCreateElement(tag);
      });

      render(<BulkImportResults result={fullSuccessResult} />);
      await user.click(screen.getByTestId('download-csv-button'));

      expect(createObjectURL).toHaveBeenCalled();
      expect(mockClick).toHaveBeenCalled();
      expect(revokeObjectURL).toHaveBeenCalled();

      vi.restoreAllMocks();
    });

    it('renders "Import Another Batch" button when onNewImport is provided', () => {
      render(
        <BulkImportResults result={fullSuccessResult} onNewImport={mockOnNewImport} />
      );

      expect(screen.getByTestId('new-import-button')).toBeInTheDocument();
      expect(screen.getByText('Import Another Batch')).toBeInTheDocument();
    });

    it('does not render "Import Another Batch" button when onNewImport is not provided', () => {
      render(<BulkImportResults result={fullSuccessResult} />);

      expect(screen.queryByTestId('new-import-button')).not.toBeInTheDocument();
    });

    it('calls onNewImport when button is clicked', async () => {
      const user = userEvent.setup();
      render(
        <BulkImportResults result={fullSuccessResult} onNewImport={mockOnNewImport} />
      );

      await user.click(screen.getByTestId('new-import-button'));
      expect(mockOnNewImport).toHaveBeenCalledTimes(1);
    });
  });

  describe('Edge Cases', () => {
    it('handles result with all failures', () => {
      render(<BulkImportResults result={allFailedResult} />);

      expect(screen.getByTestId('result-stat-total')).toHaveTextContent('3');
      expect(screen.getByTestId('result-stat-enqueued')).toHaveTextContent('0');
      expect(screen.getByTestId('result-stat-failed')).toHaveTextContent('3');
      expect(screen.getByTestId('errors-section')).toBeInTheDocument();
    });

    it('handles result with only skipped', () => {
      const skippedResult: BulkImportFromJsonResult = {
        total: 2,
        enqueued: 0,
        skipped: 2,
        failed: 0,
        errors: [],
      };
      render(<BulkImportResults result={skippedResult} />);

      expect(screen.getByTestId('result-stat-skipped')).toHaveTextContent('2');
      // isFullSuccess = failed === 0 && skipped === 0 → false (skipped=2)
      const { container } = render(<BulkImportResults result={skippedResult} />);
      const warningIcon = container.querySelector('.text-amber-500');
      expect(warningIcon).toBeInTheDocument();
    });
  });
});
