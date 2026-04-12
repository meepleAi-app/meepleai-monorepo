// apps/web/src/components/pipeline-builder/__tests__/PipelineToolbar.test.tsx

/**
 * Tests for PipelineToolbar Component
 *
 * Covers: new pipeline dialog, save/export button states, undo/redo disabled
 * states, pipeline info display, and store integration.
 *
 * Note: All toolbar action buttons are icon-only (no accessible text labels).
 * Tests identify buttons by aria attributes, SVG class names, or dialog context.
 *
 * @see Issue #3425 - Visual Pipeline Builder
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor, act, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { usePipelineBuilderStore } from '@/stores/pipelineBuilderStore';

import { PipelineToolbar } from '../PipelineToolbar';

// framer-motion is mocked globally in vitest.setup.tsx

// =============================================================================
// Browser API mocks required by the toolbar
// =============================================================================

URL.createObjectURL = vi.fn(() => 'blob:mock-url');
URL.revokeObjectURL = vi.fn();

// =============================================================================
// Store reset helper
// =============================================================================

function resetStore() {
  // Clear persisted localStorage key so Zustand's persist middleware doesn't
  // rehydrate stale state in subsequent tests.
  localStorage.removeItem('pipeline-builder-storage');
  act(() => {
    usePipelineBuilderStore.getState().clearPipeline();
  });
}

// =============================================================================
// Button selectors
// All toolbar buttons are icon-only — identified by aria attributes or SVG class
// =============================================================================

/**
 * Returns the New Pipeline dialog trigger button.
 * It is the only button with aria-haspopup="dialog".
 */
function getNewPipelineButton(): HTMLElement {
  return document.querySelector('[aria-haspopup="dialog"]') as HTMLElement;
}

/**
 * Returns the Save button.
 * In the toolbar button order: Plus (index 0), Load (index 1), Save (index 2),
 * Export (index 3), Import (index 4), Undo (index 5), Redo (index 6), ...
 * Identified by the lucide-save SVG class.
 */
function getSaveButton(): HTMLElement | null {
  const buttons = screen.getAllByRole('button');
  return buttons.find(btn => btn.querySelector('.lucide-save')) ?? null;
}

/**
 * Returns the Export (Download) button.
 * Identified by lucide-download SVG.
 */
function getExportButton(): HTMLElement | null {
  const buttons = screen.getAllByRole('button');
  return buttons.find(btn => btn.querySelector('.lucide-download')) ?? null;
}

/**
 * Returns the Undo button.
 * Identified by lucide-undo-2 SVG.
 */
function getUndoButton(): HTMLElement | null {
  const buttons = screen.getAllByRole('button');
  return buttons.find(btn => btn.querySelector('.lucide-undo-2')) ?? null;
}

// =============================================================================
// Tests
// =============================================================================

describe('PipelineToolbar', () => {
  beforeEach(() => {
    resetStore();
    vi.clearAllMocks();
    window.confirm = vi.fn(() => true);
  });

  it('renders without crashing', () => {
    render(<PipelineToolbar />);
    // The toolbar renders multiple icon buttons
    const buttons = screen.getAllByRole('button');
    expect(buttons.length).toBeGreaterThan(0);
  });

  it('New Pipeline button exists (has aria-haspopup=dialog)', () => {
    render(<PipelineToolbar />);
    const btn = getNewPipelineButton();
    expect(btn).not.toBeNull();
    expect(btn).toBeInTheDocument();
  });

  it('opens the New Pipeline dialog when the Plus button is clicked', async () => {
    const user = userEvent.setup();
    render(<PipelineToolbar />);

    const plusButton = getNewPipelineButton();
    await user.click(plusButton);

    // The dialog should now be visible
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('New Pipeline')).toBeInTheDocument();
  });

  it('keeps the Create button disabled when the pipeline name input is empty', async () => {
    const user = userEvent.setup();
    render(<PipelineToolbar />);

    await user.click(getNewPipelineButton());

    const dialog = screen.getByRole('dialog');
    const createButton = within(dialog).getByRole('button', { name: /^create$/i });
    expect(createButton).toBeDisabled();
  });

  it('enables the Create button when the user types a pipeline name', async () => {
    const user = userEvent.setup();
    render(<PipelineToolbar />);

    await user.click(getNewPipelineButton());

    const nameInput = screen.getByPlaceholderText('Pipeline name...');
    await user.type(nameInput, 'My Test Pipeline');

    const dialog = screen.getByRole('dialog');
    const createButton = within(dialog).getByRole('button', { name: /^create$/i });
    expect(createButton).not.toBeDisabled();
  });

  it('creates a pipeline and closes the dialog when Create is clicked', async () => {
    const user = userEvent.setup();
    render(<PipelineToolbar />);

    await user.click(getNewPipelineButton());

    const nameInput = screen.getByPlaceholderText('Pipeline name...');
    await user.type(nameInput, 'My Test Pipeline');

    const dialog = screen.getByRole('dialog');
    const createButton = within(dialog).getByRole('button', { name: /^create$/i });
    await user.click(createButton);

    // Dialog should be closed
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    // Pipeline should be stored
    const { pipeline } = usePipelineBuilderStore.getState();
    expect(pipeline).not.toBeNull();
    expect(pipeline!.name).toBe('My Test Pipeline');
  });

  it('Save button is disabled when no pipeline exists (initial state)', () => {
    render(<PipelineToolbar />);
    const saveBtn = getSaveButton();
    expect(saveBtn).not.toBeNull();
    expect(saveBtn).toBeDisabled();
  });

  it('Save button exists in the toolbar', () => {
    render(<PipelineToolbar />);
    expect(getSaveButton()).not.toBeNull();
  });

  it('Undo button is disabled when no history exists', () => {
    render(<PipelineToolbar />);
    const undoBtn = getUndoButton();
    expect(undoBtn).not.toBeNull();
    expect(undoBtn).toBeDisabled();
  });

  it('Export button is disabled when no pipeline exists', () => {
    render(<PipelineToolbar />);
    const exportBtn = getExportButton();
    expect(exportBtn).not.toBeNull();
    expect(exportBtn).toBeDisabled();
  });

  it('enables the Save button once a pipeline is created', async () => {
    const user = userEvent.setup();
    render(<PipelineToolbar />);

    // Open dialog and create a pipeline
    await user.click(getNewPipelineButton());
    await user.type(screen.getByPlaceholderText('Pipeline name...'), 'Test');
    const dialog = screen.getByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /^create$/i }));

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    // Save button should now be enabled (pipeline exists and not currently saving)
    const saveBtn = getSaveButton();
    expect(saveBtn).not.toBeNull();
    expect(saveBtn).not.toBeDisabled();
  });

  it('shows pipeline name and node/edge badges once pipeline is created', async () => {
    const user = userEvent.setup();
    render(<PipelineToolbar />);

    await user.click(getNewPipelineButton());
    await user.type(screen.getByPlaceholderText('Pipeline name...'), 'Badge Test Pipeline');
    const dialog = screen.getByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /^create$/i }));

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    expect(screen.getByText('Badge Test Pipeline')).toBeInTheDocument();
    expect(screen.getByText('0 nodes')).toBeInTheDocument();
    expect(screen.getByText('0 edges')).toBeInTheDocument();
  });
});
