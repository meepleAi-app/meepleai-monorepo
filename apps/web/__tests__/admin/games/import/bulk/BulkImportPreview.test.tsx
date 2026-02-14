/**
 * BulkImportPreview Component & parseAndValidateEntries Tests - Issue #4356
 *
 * Test coverage:
 * - parseAndValidateEntries function (unit tests)
 * - BulkImportPreview component rendering
 * - Validation summary display
 * - Preview table display
 * - Error details display
 * - Confirm/cancel actions
 * - Edge cases (duplicates, invalid entries, max entries)
 *
 * Target: >= 85% coverage
 */

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  BulkImportPreview,
  parseAndValidateEntries,
  type PreviewData,
} from '@/app/(authenticated)/admin/games/import/bulk/BulkImportPreview';

// ─── parseAndValidateEntries Tests ───────────────────────────────

describe('parseAndValidateEntries', () => {
  it('returns null for invalid JSON', () => {
    expect(parseAndValidateEntries('not json')).toBeNull();
  });

  it('returns null for non-array JSON', () => {
    expect(parseAndValidateEntries('{"key": "value"}')).toBeNull();
  });

  it('returns null for empty array', () => {
    expect(parseAndValidateEntries('[]')).toBeNull();
  });

  it('parses valid entries correctly', () => {
    const json = JSON.stringify([
      { bggId: 174430, name: 'Gloomhaven' },
      { bggId: 167791, name: 'Terraforming Mars' },
      { bggId: 169786, name: 'Scythe' },
    ]);
    const result = parseAndValidateEntries(json);

    expect(result).not.toBeNull();
    expect(result!.totalEntries).toBe(3);
    expect(result!.validCount).toBe(3);
    expect(result!.invalidCount).toBe(0);
    expect(result!.duplicateCount).toBe(0);
    expect(result!.entries).toHaveLength(3);
    expect(result!.entries[0]).toEqual({
      index: 0,
      bggId: 174430,
      name: 'Gloomhaven',
      isValid: true,
      errors: [],
      isDuplicate: false,
    });
  });

  it('detects invalid bggId (not a number)', () => {
    const json = JSON.stringify([{ bggId: 'abc', name: 'Test' }]);
    const result = parseAndValidateEntries(json);

    expect(result!.invalidCount).toBe(1);
    expect(result!.validCount).toBe(0);
    expect(result!.entries[0].isValid).toBe(false);
    expect(result!.entries[0].errors).toContain('bggId must be a positive integer');
  });

  it('detects invalid bggId (zero)', () => {
    const json = JSON.stringify([{ bggId: 0, name: 'Test' }]);
    const result = parseAndValidateEntries(json);

    expect(result!.entries[0].isValid).toBe(false);
    expect(result!.entries[0].errors).toContain('bggId must be a positive integer');
  });

  it('detects invalid bggId (negative)', () => {
    const json = JSON.stringify([{ bggId: -5, name: 'Test' }]);
    const result = parseAndValidateEntries(json);

    expect(result!.entries[0].isValid).toBe(false);
  });

  it('detects invalid bggId (float)', () => {
    const json = JSON.stringify([{ bggId: 1.5, name: 'Test' }]);
    const result = parseAndValidateEntries(json);

    expect(result!.entries[0].isValid).toBe(false);
  });

  it('detects empty name', () => {
    const json = JSON.stringify([{ bggId: 1, name: '' }]);
    const result = parseAndValidateEntries(json);

    expect(result!.entries[0].isValid).toBe(false);
    expect(result!.entries[0].errors).toContain('name must be a non-empty string');
  });

  it('detects missing name', () => {
    const json = JSON.stringify([{ bggId: 1 }]);
    const result = parseAndValidateEntries(json);

    expect(result!.entries[0].isValid).toBe(false);
    expect(result!.entries[0].errors).toContain('name must be a non-empty string');
  });

  it('detects non-object entries', () => {
    const json = JSON.stringify([42, 'string', null]);
    const result = parseAndValidateEntries(json);

    expect(result!.invalidCount).toBe(3);
    expect(result!.entries[0].errors).toContain('Entry is not an object');
    expect(result!.entries[2].errors).toContain('Entry is not an object');
  });

  it('detects duplicate bggIds within batch', () => {
    const json = JSON.stringify([
      { bggId: 174430, name: 'Gloomhaven' },
      { bggId: 167791, name: 'Terraforming Mars' },
      { bggId: 174430, name: 'Gloomhaven Copy' },
    ]);
    const result = parseAndValidateEntries(json);

    expect(result!.duplicateCount).toBe(1);
    expect(result!.validCount).toBe(2);
    expect(result!.entries[2].isDuplicate).toBe(true);
    expect(result!.entries[2].errors[0]).toContain('Duplicate of entry #1');
  });

  it('handles mix of valid, invalid, and duplicate entries', () => {
    const json = JSON.stringify([
      { bggId: 1, name: 'Valid Game' },
      { bggId: -1, name: 'Invalid' },
      { bggId: 1, name: 'Duplicate' },
      { bggId: 2, name: '' },
    ]);
    const result = parseAndValidateEntries(json);

    expect(result!.totalEntries).toBe(4);
    expect(result!.validCount).toBe(1);
    expect(result!.invalidCount).toBe(2);
    expect(result!.duplicateCount).toBe(1);
  });

  it('trims whitespace from names', () => {
    const json = JSON.stringify([{ bggId: 1, name: '  Gloomhaven  ' }]);
    const result = parseAndValidateEntries(json);

    expect(result!.entries[0].name).toBe('Gloomhaven');
    expect(result!.entries[0].isValid).toBe(true);
  });

  it('treats whitespace-only name as invalid', () => {
    const json = JSON.stringify([{ bggId: 1, name: '   ' }]);
    const result = parseAndValidateEntries(json);

    expect(result!.entries[0].isValid).toBe(false);
    expect(result!.entries[0].errors).toContain('name must be a non-empty string');
  });
});

// ─── BulkImportPreview Component Tests ───────────────────────────

describe('BulkImportPreview', () => {
  const mockOnConfirm = vi.fn();
  const mockOnBack = vi.fn();

  const validPreviewData: PreviewData = {
    entries: [
      { index: 0, bggId: 174430, name: 'Gloomhaven', isValid: true, errors: [], isDuplicate: false },
      { index: 1, bggId: 167791, name: 'Terraforming Mars', isValid: true, errors: [], isDuplicate: false },
      { index: 2, bggId: 169786, name: 'Scythe', isValid: true, errors: [], isDuplicate: false },
    ],
    totalEntries: 3,
    validCount: 3,
    invalidCount: 0,
    duplicateCount: 0,
  };

  const mixedPreviewData: PreviewData = {
    entries: [
      { index: 0, bggId: 174430, name: 'Gloomhaven', isValid: true, errors: [], isDuplicate: false },
      { index: 1, bggId: 0, name: '', isValid: false, errors: ['bggId must be a positive integer', 'name must be a non-empty string'], isDuplicate: false },
      { index: 2, bggId: 174430, name: 'Gloomhaven Copy', isValid: false, errors: ['Duplicate of entry #1'], isDuplicate: true },
    ],
    totalEntries: 3,
    validCount: 1,
    invalidCount: 1,
    duplicateCount: 1,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders the preview container', () => {
      render(
        <BulkImportPreview
          previewData={validPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      expect(screen.getByTestId('bulk-import-preview')).toBeInTheDocument();
    });

    it('renders the preview header with back button', () => {
      render(
        <BulkImportPreview
          previewData={validPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      expect(screen.getByText('Import Preview')).toBeInTheDocument();
      expect(screen.getByTestId('preview-back')).toBeInTheDocument();
    });

    it('renders the validation summary card', () => {
      render(
        <BulkImportPreview
          previewData={validPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      expect(screen.getByTestId('validation-summary')).toBeInTheDocument();
      expect(screen.getByText('Validation Summary')).toBeInTheDocument();
    });

    it('renders the preview table', () => {
      render(
        <BulkImportPreview
          previewData={validPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      expect(screen.getByTestId('preview-table')).toBeInTheDocument();
    });
  });

  describe('Validation Summary', () => {
    it('displays correct counts for all-valid entries', () => {
      render(
        <BulkImportPreview
          previewData={validPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      expect(screen.getByTestId('total-count')).toHaveTextContent('3');
      expect(screen.getByTestId('valid-count')).toHaveTextContent('3');
      expect(screen.getByTestId('duplicate-count')).toHaveTextContent('0');
      expect(screen.getByTestId('invalid-count')).toHaveTextContent('0');
    });

    it('displays correct counts for mixed entries', () => {
      render(
        <BulkImportPreview
          previewData={mixedPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      expect(screen.getByTestId('total-count')).toHaveTextContent('3');
      expect(screen.getByTestId('valid-count')).toHaveTextContent('1');
      expect(screen.getByTestId('duplicate-count')).toHaveTextContent('1');
      expect(screen.getByTestId('invalid-count')).toHaveTextContent('1');
    });

    it('shows all-valid alert when no errors', () => {
      render(
        <BulkImportPreview
          previewData={validPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      expect(screen.getByTestId('all-valid-alert')).toBeInTheDocument();
      expect(screen.getByText(/3 games ready to import/)).toBeInTheDocument();
    });

    it('shows error alert when entries have issues', () => {
      render(
        <BulkImportPreview
          previewData={mixedPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      expect(screen.getByTestId('has-errors-alert')).toBeInTheDocument();
      expect(screen.getByText(/1 invalid entry will be skipped/)).toBeInTheDocument();
      expect(screen.getByText(/1 duplicate entry detected/)).toBeInTheDocument();
    });

    it('shows limit exceeded alert when over 500 entries', () => {
      const largeData: PreviewData = {
        entries: Array.from({ length: 501 }, (_, i) => ({
          index: i,
          bggId: i + 1,
          name: `Game ${i + 1}`,
          isValid: true,
          errors: [],
          isDuplicate: false,
        })),
        totalEntries: 501,
        validCount: 501,
        invalidCount: 0,
        duplicateCount: 0,
      };
      render(
        <BulkImportPreview
          previewData={largeData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      expect(screen.getByTestId('limit-exceeded-alert')).toBeInTheDocument();
      expect(screen.getByText(/Maximum 500 entries per import/)).toBeInTheDocument();
    });
  });

  describe('Preview Table', () => {
    it('displays entry rows with correct data', () => {
      render(
        <BulkImportPreview
          previewData={validPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      expect(screen.getByTestId('preview-row-0')).toBeInTheDocument();
      expect(screen.getByTestId('preview-row-1')).toBeInTheDocument();
      expect(screen.getByTestId('preview-row-2')).toBeInTheDocument();
      expect(screen.getByText('Gloomhaven')).toBeInTheDocument();
      expect(screen.getByText('174430')).toBeInTheDocument();
    });

    it('shows Valid badge for valid entries', () => {
      render(
        <BulkImportPreview
          previewData={validPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      const table = screen.getByTestId('preview-table');
      const validBadges = within(table).getAllByText('Valid');
      expect(validBadges).toHaveLength(3);
    });

    it('shows Duplicate badge for duplicate entries', () => {
      render(
        <BulkImportPreview
          previewData={mixedPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      const dupRow = screen.getByTestId('preview-row-2');
      expect(within(dupRow).getByText('Duplicate')).toBeInTheDocument();
    });

    it('shows Invalid badge for invalid entries', () => {
      render(
        <BulkImportPreview
          previewData={mixedPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      const invalidRow = screen.getByTestId('preview-row-1');
      expect(within(invalidRow).getByText('Invalid')).toBeInTheDocument();
    });

    it('limits display to 20 entries and shows remaining count', () => {
      const manyEntries: PreviewData = {
        entries: Array.from({ length: 25 }, (_, i) => ({
          index: i,
          bggId: i + 1,
          name: `Game ${i + 1}`,
          isValid: true,
          errors: [],
          isDuplicate: false,
        })),
        totalEntries: 25,
        validCount: 25,
        invalidCount: 0,
        duplicateCount: 0,
      };
      render(
        <BulkImportPreview
          previewData={manyEntries}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      // Should show 20 rows
      expect(screen.getByTestId('preview-row-0')).toBeInTheDocument();
      expect(screen.getByTestId('preview-row-19')).toBeInTheDocument();
      expect(screen.queryByTestId('preview-row-20')).not.toBeInTheDocument();
      expect(screen.getByText(/\+5 more entries not shown/)).toBeInTheDocument();
    });

    it('shows "Showing X of Y" badge', () => {
      render(
        <BulkImportPreview
          previewData={validPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      expect(screen.getByText('Showing 3 of 3')).toBeInTheDocument();
    });
  });

  describe('Error Details', () => {
    it('shows error details section when entries have errors', () => {
      render(
        <BulkImportPreview
          previewData={mixedPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      expect(screen.getByTestId('error-details')).toBeInTheDocument();
      expect(screen.getByText(/Issues Found/)).toBeInTheDocument();
    });

    it('does not show error details when all entries are valid', () => {
      render(
        <BulkImportPreview
          previewData={validPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      expect(screen.queryByTestId('error-details')).not.toBeInTheDocument();
    });

    it('lists specific error messages', () => {
      render(
        <BulkImportPreview
          previewData={mixedPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      expect(screen.getByText('bggId must be a positive integer')).toBeInTheDocument();
      expect(screen.getByText('name must be a non-empty string')).toBeInTheDocument();
      expect(screen.getByText('Duplicate of entry #1')).toBeInTheDocument();
    });
  });

  describe('Action Buttons', () => {
    it('renders confirm import button with valid count', () => {
      render(
        <BulkImportPreview
          previewData={validPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      const confirmBtn = screen.getByTestId('confirm-import');
      expect(confirmBtn).toBeInTheDocument();
      expect(confirmBtn).toHaveTextContent('Confirm Import (3 games)');
      expect(confirmBtn).not.toBeDisabled();
    });

    it('calls onConfirm when confirm button is clicked', async () => {
      const user = userEvent.setup();
      render(
        <BulkImportPreview
          previewData={validPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      await user.click(screen.getByTestId('confirm-import'));
      expect(mockOnConfirm).toHaveBeenCalledTimes(1);
    });

    it('calls onBack when back button is clicked', async () => {
      const user = userEvent.setup();
      render(
        <BulkImportPreview
          previewData={validPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      await user.click(screen.getByTestId('preview-back'));
      expect(mockOnBack).toHaveBeenCalledTimes(1);
    });

    it('calls onBack when cancel button is clicked', async () => {
      const user = userEvent.setup();
      render(
        <BulkImportPreview
          previewData={validPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      await user.click(screen.getByTestId('preview-cancel'));
      expect(mockOnBack).toHaveBeenCalledTimes(1);
    });

    it('disables buttons when isSubmitting is true', () => {
      render(
        <BulkImportPreview
          previewData={validPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={true}
        />
      );
      expect(screen.getByTestId('confirm-import')).toBeDisabled();
      expect(screen.getByTestId('preview-back')).toBeDisabled();
      expect(screen.getByTestId('preview-cancel')).toBeDisabled();
    });

    it('shows "Importing..." text when isSubmitting is true', () => {
      render(
        <BulkImportPreview
          previewData={validPreviewData}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={true}
        />
      );
      expect(screen.getByTestId('confirm-import')).toHaveTextContent('Importing...');
    });

    it('disables confirm when no valid entries', () => {
      const allInvalid: PreviewData = {
        entries: [
          { index: 0, bggId: 0, name: '', isValid: false, errors: ['invalid'], isDuplicate: false },
        ],
        totalEntries: 1,
        validCount: 0,
        invalidCount: 1,
        duplicateCount: 0,
      };
      render(
        <BulkImportPreview
          previewData={allInvalid}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      expect(screen.getByTestId('confirm-import')).toBeDisabled();
    });

    it('disables confirm when entries exceed limit', () => {
      const overLimit: PreviewData = {
        entries: Array.from({ length: 501 }, (_, i) => ({
          index: i,
          bggId: i + 1,
          name: `Game ${i + 1}`,
          isValid: true,
          errors: [],
          isDuplicate: false,
        })),
        totalEntries: 501,
        validCount: 501,
        invalidCount: 0,
        duplicateCount: 0,
      };
      render(
        <BulkImportPreview
          previewData={overLimit}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      expect(screen.getByTestId('confirm-import')).toBeDisabled();
    });

    it('uses singular "game" when only 1 valid entry', () => {
      const singleEntry: PreviewData = {
        entries: [
          { index: 0, bggId: 1, name: 'Solo Game', isValid: true, errors: [], isDuplicate: false },
        ],
        totalEntries: 1,
        validCount: 1,
        invalidCount: 0,
        duplicateCount: 0,
      };
      render(
        <BulkImportPreview
          previewData={singleEntry}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      expect(screen.getByTestId('confirm-import')).toHaveTextContent('Confirm Import (1 game)');
    });
  });

  describe('Entry Preview showing dash for missing values', () => {
    it('shows dash for bggId 0 and empty name', () => {
      const dataWithMissing: PreviewData = {
        entries: [
          { index: 0, bggId: 0, name: '', isValid: false, errors: ['invalid'], isDuplicate: false },
        ],
        totalEntries: 1,
        validCount: 0,
        invalidCount: 1,
        duplicateCount: 0,
      };
      render(
        <BulkImportPreview
          previewData={dataWithMissing}
          onConfirm={mockOnConfirm}
          onBack={mockOnBack}
          isSubmitting={false}
        />
      );
      const row = screen.getByTestId('preview-row-0');
      const cells = within(row).getAllByRole('cell');
      expect(cells[1]).toHaveTextContent('-');
      expect(cells[2]).toHaveTextContent('-');
    });
  });
});
