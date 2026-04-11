// apps/web/src/components/pipeline-builder/__tests__/PluginPalette.test.tsx

/**
 * Tests for PluginPalette Component
 *
 * Covers: search, category filtering, favorites, drag-start, empty state,
 * footer text, and plugin rendering.
 *
 * @see Issue #3426 - Plugin Palette Component
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { usePipelineBuilderStore } from '@/stores/pipelineBuilderStore';

import { PluginPalette } from '../PluginPalette';
import { BUILT_IN_PLUGINS } from '../types';

// framer-motion is mocked globally in vitest.setup.tsx

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

describe('PluginPalette', () => {
  beforeEach(() => {
    resetStore();
    vi.clearAllMocks();
  });

  it('renders the search input with correct placeholder', () => {
    render(<PluginPalette />);
    expect(screen.getByPlaceholderText('Search plugins...')).toBeInTheDocument();
  });

  it('renders all built-in plugin names', () => {
    render(<PluginPalette />);
    for (const plugin of BUILT_IN_PLUGINS) {
      expect(screen.getByText(plugin.name)).toBeInTheDocument();
    }
  });

  it('renders the "All" category filter button', () => {
    render(<PluginPalette />);
    expect(screen.getByRole('button', { name: 'All' })).toBeInTheDocument();
  });

  it('renders the footer drag hint text', () => {
    render(<PluginPalette />);
    expect(
      screen.getByText('Drag plugins onto the canvas to build your pipeline')
    ).toBeInTheDocument();
  });

  it('filters plugins by search query', async () => {
    const user = userEvent.setup();
    render(<PluginPalette />);

    const input = screen.getByPlaceholderText('Search plugins...');
    await user.type(input, 'LLM Router');

    // LLM Router should be visible
    expect(screen.getByText('LLM Router')).toBeInTheDocument();

    // Other unrelated plugins should not be visible
    expect(screen.queryByText('Semantic Cache')).not.toBeInTheDocument();
    expect(screen.queryByText('Hybrid Retrieval')).not.toBeInTheDocument();
  });

  it('shows empty state message when search yields no results', async () => {
    const user = userEvent.setup();
    render(<PluginPalette />);

    const input = screen.getByPlaceholderText('Search plugins...');
    await user.type(input, 'zzznonexistent9999');

    expect(screen.getByText(/No plugins found matching "zzznonexistent9999"/)).toBeInTheDocument();
  });

  it('filters to routing plugins when the routing category button is clicked', async () => {
    const user = userEvent.setup();
    render(<PluginPalette />);

    // The category filter bar renders lowercase category names (e.g. "🧠 routing").
    // The CollapsibleTrigger section header renders "Routing" (capitalized label).
    // Use getAllByRole and pick the first match which is in the filter bar.
    const routingButtons = screen.getAllByRole('button', { name: /routing/i });
    // Filter bar button renders the raw lowercase "routing" text, section header renders "Routing"
    // The first match is the category filter button
    const routingFilterButton = routingButtons[0];
    await user.click(routingFilterButton);

    // Routing plugins should be visible
    const routingPlugins = BUILT_IN_PLUGINS.filter(p => p.category === 'routing');
    for (const plugin of routingPlugins) {
      expect(screen.getByText(plugin.name)).toBeInTheDocument();
    }

    // Non-routing plugins should not be visible
    const nonRoutingPlugin = BUILT_IN_PLUGINS.find(p => p.category !== 'routing')!;
    expect(screen.queryByText(nonRoutingPlugin.name)).not.toBeInTheDocument();
  });

  it('restores all plugins when "All" button is clicked after a category filter', async () => {
    const user = userEvent.setup();
    render(<PluginPalette />);

    // Apply a cache category filter. Filter bar buttons contain the raw category name
    // (e.g. "💾 cache"). Avoid using CSS class selectors — match by text content only.
    const cacheButton = screen
      .getAllByRole('button')
      .find(btn => /cache/i.test(btn.textContent ?? '') && !/all/i.test(btn.textContent ?? ''));
    expect(cacheButton).toBeDefined();
    await user.click(cacheButton!);

    // Confirm filter is active — routing plugins should be hidden
    const routingPlugin = BUILT_IN_PLUGINS.find(p => p.category === 'routing')!;
    expect(screen.queryByText(routingPlugin.name)).not.toBeInTheDocument();

    // Click "All" to reset
    const allButton = screen.getByRole('button', { name: 'All' });
    await user.click(allButton);

    // All plugins visible again
    for (const plugin of BUILT_IN_PLUGINS) {
      expect(screen.getByText(plugin.name)).toBeInTheDocument();
    }
  });

  it('marks a plugin as favorite when the star button is clicked', async () => {
    const user = userEvent.setup();
    render(<PluginPalette />);

    // Each PluginCard has a star button (ghost, h-6 w-6 p-0) and an info button.
    // Find the first plugin card by its name and locate the star button within it.
    const firstPluginName = BUILT_IN_PLUGINS[0].name;
    const firstPluginNameEl = screen.getByText(firstPluginName);
    // Navigate up to the card container and find buttons within it
    const cardContainer = firstPluginNameEl.closest('.group') as HTMLElement;
    expect(cardContainer).not.toBeNull();

    const buttonsInCard = Array.from(cardContainer.querySelectorAll('button'));
    // First button in the card header is the star toggle button
    const starButton = buttonsInCard[0];
    await user.click(starButton);

    // After clicking, the Favorites section header text should appear
    expect(screen.getByText('Favorites')).toBeInTheDocument();
  });

  it('calls startDrag when a draggable card fires dragstart', () => {
    render(<PluginPalette />);

    // Target a specific known plugin to avoid coupling to DOM order or CATEGORY_ORDER.
    const targetPlugin = BUILT_IN_PLUGINS[0];
    const pluginNameEl = screen.getByText(targetPlugin.name);
    // Walk up to the nearest [draggable] ancestor
    const draggableCard = pluginNameEl.closest('[draggable]') as HTMLElement;
    expect(draggableCard).not.toBeNull();

    // Provide a mock dataTransfer to satisfy jsdom (which does not implement it fully)
    fireEvent.dragStart(draggableCard, {
      dataTransfer: {
        effectAllowed: '',
        setData: vi.fn(),
      },
    });

    // The store should now reflect the drag state for this specific plugin
    const { isDragging, draggedPlugin } = usePipelineBuilderStore.getState();
    expect(isDragging).toBe(true);
    expect(draggedPlugin).not.toBeNull();
    expect(draggedPlugin!.id).toBe(targetPlugin.id);
  });

  it('renders category section headers for each category present', () => {
    render(<PluginPalette />);
    // The CategorySection renders CATEGORY_LABELS keys. Check a few.
    // CollapsibleTrigger renders the label text.
    expect(screen.getByText('Routing')).toBeInTheDocument();
    expect(screen.getByText('Caching')).toBeInTheDocument();
    expect(screen.getByText('Retrieval')).toBeInTheDocument();
  });

  it('shows plugin descriptions in the rendered cards', () => {
    render(<PluginPalette />);
    // Each PluginCard renders plugin.description
    const firstPlugin = BUILT_IN_PLUGINS[0];
    expect(screen.getByText(firstPlugin.description)).toBeInTheDocument();
  });

  it('renders a custom plugin list when the plugins prop is provided', () => {
    const customPlugin = BUILT_IN_PLUGINS[0];
    render(<PluginPalette plugins={[customPlugin]} />);

    expect(screen.getByText(customPlugin.name)).toBeInTheDocument();
    // Other default plugins should NOT be rendered
    const otherPlugin = BUILT_IN_PLUGINS[1];
    expect(screen.queryByText(otherPlugin.name)).not.toBeInTheDocument();
  });
});
