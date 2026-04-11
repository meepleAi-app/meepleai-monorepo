// apps/web/src/components/pipeline-builder/__tests__/PipelineBuilder.test.tsx

/**
 * Tests for PipelineBuilder Component
 *
 * Covers: initial render, auto-pipeline creation on mount, tab switching,
 * panel structure, toolbar and palette presence, default config state.
 *
 * @see Issue #3425 - Visual Pipeline Builder
 */

import React from 'react';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { usePipelineBuilderStore } from '@/stores/pipelineBuilderStore';

import { PipelineBuilder } from '../PipelineBuilder';

// framer-motion is mocked globally in vitest.setup.tsx

// =============================================================================
// Mock react-resizable-panels
// The component imports from @/components/ui/primitives/resizable which wraps
// react-resizable-panels. We mock the underlying package.
// ResizablePanel accepts a `panelRef` and `onResize` prop — strip them to avoid
// unknown-prop React warnings.
// =============================================================================

vi.mock('react-resizable-panels', () => ({
  Group: ({ children, ...props }: { children: React.ReactNode; [key: string]: unknown }) => {
    const { className } = props as { className?: string };
    return React.createElement('div', { 'data-testid': 'resizable-group', className }, children);
  },
  Panel: ({
    children,
    panelRef: _panelRef,
    onResize: _onResize,
    defaultSize: _ds,
    minSize: _ms,
    maxSize: _mxs,
    collapsible: _c,
    collapsedSize: _cs,
    ...props
  }: {
    children?: React.ReactNode;
    panelRef?: unknown;
    onResize?: unknown;
    defaultSize?: unknown;
    minSize?: unknown;
    maxSize?: unknown;
    collapsible?: unknown;
    collapsedSize?: unknown;
    [key: string]: unknown;
  }) => {
    const { className } = props as { className?: string };
    return React.createElement('div', { 'data-testid': 'resizable-panel', className }, children);
  },
  Separator: ({
    withHandle: _wh,
    children,
    ...props
  }: {
    withHandle?: boolean;
    children?: React.ReactNode;
    [key: string]: unknown;
  }) => React.createElement('div', { 'data-testid': 'resizable-handle', ...props }, children),
}));

// =============================================================================
// Mock PipelineCanvas and PipelinePreview — heavy React Flow dependencies
// =============================================================================

vi.mock('../PipelineCanvas', () => ({
  PipelineCanvas: vi.fn(({ className }: { className?: string }) =>
    React.createElement('div', { 'data-testid': 'mock-canvas', className })
  ),
}));

vi.mock('../PipelinePreview', () => ({
  PipelinePreview: vi.fn(({ className }: { className?: string }) =>
    React.createElement('div', { 'data-testid': 'mock-preview', className })
  ),
}));

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
// Tests
// =============================================================================

describe('PipelineBuilder', () => {
  beforeEach(() => {
    resetStore();
    vi.clearAllMocks();
  });

  it('renders without crashing', () => {
    render(<PipelineBuilder />);
    // The toolbar is always present — confirms the component mounted successfully
    expect(document.querySelector('[aria-haspopup="dialog"]')).toBeInTheDocument();
  });

  it('auto-creates a pipeline on mount when none exists', async () => {
    // Confirm store has no pipeline before render
    expect(usePipelineBuilderStore.getState().pipeline).toBeNull();

    render(<PipelineBuilder />);

    await waitFor(() => {
      const { pipeline } = usePipelineBuilderStore.getState();
      expect(pipeline).not.toBeNull();
      expect(pipeline!.name).toBe('New Pipeline');
      expect(pipeline!.description).toBe('A new RAG pipeline');
    });
  });

  it('does not create a second pipeline when one already exists', async () => {
    // Pre-populate the store
    act(() => {
      usePipelineBuilderStore.getState().createPipeline('Existing Pipeline');
    });
    const { pipeline: before } = usePipelineBuilderStore.getState();
    const existingId = before!.id;

    render(<PipelineBuilder />);

    // Yield to useEffect
    await waitFor(() => {
      const { pipeline: after } = usePipelineBuilderStore.getState();
      expect(after!.id).toBe(existingId);
      expect(after!.name).toBe('Existing Pipeline');
    });
  });

  it('renders the PipelineToolbar', () => {
    render(<PipelineBuilder />);
    // PipelineToolbar renders the New Pipeline dialog trigger button (aria-haspopup="dialog")
    const newPipelineBtn = document.querySelector('[aria-haspopup="dialog"]');
    expect(newPipelineBtn).toBeInTheDocument();
  });

  it('renders the "Plugins" section heading in the left panel', async () => {
    render(<PipelineBuilder />);
    // The left panel header renders <h3>Plugins</h3>
    expect(screen.getByText('Plugins')).toBeInTheDocument();
  });

  it('renders the "Config" tab trigger', () => {
    render(<PipelineBuilder />);
    expect(screen.getByRole('tab', { name: /config/i })).toBeInTheDocument();
  });

  it('renders the "Test" tab trigger', () => {
    render(<PipelineBuilder />);
    expect(screen.getByRole('tab', { name: /test/i })).toBeInTheDocument();
  });

  it('shows "Select a node or edge to configure" when config tab is active and nothing is selected', async () => {
    render(<PipelineBuilder />);

    await waitFor(() => {
      expect(screen.getByText('Select a node or edge to configure')).toBeInTheDocument();
    });
  });

  it('switches to the Test panel content when the Test tab is clicked', async () => {
    const user = userEvent.setup();
    render(<PipelineBuilder />);

    const testTab = screen.getByRole('tab', { name: /test/i });
    await user.click(testTab);

    // After clicking Test tab, PipelinePreview (mock-preview) should be rendered
    await waitFor(() => {
      expect(screen.getByTestId('mock-preview')).toBeInTheDocument();
    });

    // Config placeholder should be gone
    expect(screen.queryByText('Select a node or edge to configure')).not.toBeInTheDocument();
  });

  it('renders the canvas mock', async () => {
    render(<PipelineBuilder />);
    expect(screen.getByTestId('mock-canvas')).toBeInTheDocument();
  });

  it('renders the PluginPalette search input inside the left panel', async () => {
    render(<PipelineBuilder />);
    // PluginPalette renders a search input
    expect(screen.getByPlaceholderText('Search plugins...')).toBeInTheDocument();
  });
});
