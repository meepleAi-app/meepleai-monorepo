/**
 * ToolRail Component Tests (Issue #4973)
 *
 * Coverage:
 * - Renders base tools
 * - Active tool highlighted (aria-selected)
 * - Calls onToolChange on click
 * - Keyboard navigation (ArrowDown/ArrowUp for desktop, left/right for mobile)
 * - Separator shown only when custom tools exist
 * - Collapse toggle present on desktop
 * - Accessibility: role="tablist", aria-label, aria-selected
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ToolRail, BASE_TOOLS } from '../ToolRail';
import type { ToolItem } from '../ToolRail';

// ── matchMedia stub (jsdom doesn't implement it) ──────────────────────────
beforeEach(() => {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: false,
    media: query,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  }));
});

// ── helpers ───────────────────────────────────────────────────────────────
function makeCustomTool(id: string): ToolItem {
  return {
    id,
    icon: <span data-testid={`icon-${id}`}>★</span>,
    label: `Custom ${id}`,
    shortLabel: id,
    type: 'custom',
  };
}

describe('ToolRail', () => {
  it('renders all base tool buttons', () => {
    render(
      <ToolRail
        tools={BASE_TOOLS}
        activeTool="scoreboard"
        onToolChange={vi.fn()}
      />
    );

    // Both desktop tablist and mobile tablist render the same tools
    const tabbables = screen.getAllByRole('tab', { name: 'Punteggi' });
    expect(tabbables.length).toBeGreaterThan(0);
    expect(screen.getAllByRole('tab', { name: 'Ordine turno' }).length).toBeGreaterThan(0);
    expect(screen.getAllByRole('tab', { name: 'Dadi' }).length).toBeGreaterThan(0);
    expect(screen.getAllByRole('tab', { name: 'Lavagna' }).length).toBeGreaterThan(0);
  });

  it('marks active tool with aria-selected=true', () => {
    render(
      <ToolRail
        tools={BASE_TOOLS}
        activeTool="dice"
        onToolChange={vi.fn()}
      />
    );

    const diceButtons = screen.getAllByRole('tab', { name: 'Dadi' });
    diceButtons.forEach(btn => {
      expect(btn).toHaveAttribute('aria-selected', 'true');
    });
    const scoreButtons = screen.getAllByRole('tab', { name: 'Punteggi' });
    scoreButtons.forEach(btn => {
      expect(btn).toHaveAttribute('aria-selected', 'false');
    });
  });

  it('calls onToolChange with tool id on click', async () => {
    const user = userEvent.setup();
    const onToolChange = vi.fn();

    render(
      <ToolRail
        tools={BASE_TOOLS}
        activeTool="scoreboard"
        onToolChange={onToolChange}
      />
    );

    const diceButtons = screen.getAllByRole('tab', { name: 'Dadi' });
    await user.click(diceButtons[0]);
    expect(onToolChange).toHaveBeenCalledWith('dice');
  });

  it('does NOT render separator when no custom tools', () => {
    render(
      <ToolRail
        tools={BASE_TOOLS}
        activeTool="scoreboard"
        onToolChange={vi.fn()}
      />
    );

    // The separator container should not be in the DOM
    expect(document.querySelector('[title="Tool custom"]')).toBeNull();
  });

  it('renders separator between base and custom tools', () => {
    const tools: ToolItem[] = [...BASE_TOOLS, makeCustomTool('timer')];
    render(
      <ToolRail
        tools={tools}
        activeTool="scoreboard"
        onToolChange={vi.fn()}
      />
    );

    expect(document.querySelector('[title="Tool custom"]')).not.toBeNull();
  });

  it('renders custom tool buttons', () => {
    const tools: ToolItem[] = [...BASE_TOOLS, makeCustomTool('timer'), makeCustomTool('counter')];
    render(
      <ToolRail
        tools={tools}
        activeTool="scoreboard"
        onToolChange={vi.fn()}
      />
    );

    expect(screen.getAllByRole('tab', { name: 'Custom timer' }).length).toBeGreaterThan(0);
    expect(screen.getAllByRole('tab', { name: 'Custom counter' }).length).toBeGreaterThan(0);
  });

  it('desktop tablist has aria-orientation=vertical', () => {
    render(
      <ToolRail
        tools={BASE_TOOLS}
        activeTool="scoreboard"
        onToolChange={vi.fn()}
      />
    );

    const tablists = screen.getAllByRole('tablist');
    const desktop = tablists.find(t => t.getAttribute('aria-orientation') === 'vertical');
    expect(desktop).toBeDefined();
  });

  it('mobile tablist has aria-orientation=horizontal', () => {
    render(
      <ToolRail
        tools={BASE_TOOLS}
        activeTool="scoreboard"
        onToolChange={vi.fn()}
      />
    );

    const tablists = screen.getAllByRole('tablist');
    const mobile = tablists.find(t => t.getAttribute('aria-orientation') === 'horizontal');
    expect(mobile).toBeDefined();
  });

  it('ArrowDown on desktop tablist cycles to next tool', () => {
    const onToolChange = vi.fn();
    render(
      <ToolRail
        tools={BASE_TOOLS}
        activeTool="scoreboard"
        onToolChange={onToolChange}
      />
    );

    // Find the vertical (desktop) tablist
    const tablists = screen.getAllByRole('tablist');
    const desktopRail = tablists.find(t => t.getAttribute('aria-orientation') === 'vertical')!;

    fireEvent.keyDown(desktopRail, { key: 'ArrowDown' });
    // scoreboard is index 0, next should be turn-order (index 1)
    expect(onToolChange).toHaveBeenCalledWith('turn-order');
  });

  it('ArrowUp on desktop tablist cycles to previous tool (wrapping)', () => {
    const onToolChange = vi.fn();
    render(
      <ToolRail
        tools={BASE_TOOLS}
        activeTool="scoreboard"
        onToolChange={onToolChange}
      />
    );

    const tablists = screen.getAllByRole('tablist');
    const desktopRail = tablists.find(t => t.getAttribute('aria-orientation') === 'vertical')!;

    fireEvent.keyDown(desktopRail, { key: 'ArrowUp' });
    // scoreboard is index 0, previous wraps to last (camera, index 4)
    expect(onToolChange).toHaveBeenCalledWith('camera');
  });

  it('ArrowRight on mobile tablist cycles to next tool', () => {
    const onToolChange = vi.fn();
    render(
      <ToolRail
        tools={BASE_TOOLS}
        activeTool="dice"
        onToolChange={onToolChange}
      />
    );

    const tablists = screen.getAllByRole('tablist');
    const mobileNav = tablists.find(t => t.getAttribute('aria-orientation') === 'horizontal')!;

    fireEvent.keyDown(mobileNav, { key: 'ArrowRight' });
    // dice is index 2, next is whiteboard (index 3)
    expect(onToolChange).toHaveBeenCalledWith('whiteboard');
  });

  it('Enter key triggers onToolChange', async () => {
    const user = userEvent.setup();
    const onToolChange = vi.fn();
    render(
      <ToolRail
        tools={BASE_TOOLS}
        activeTool="scoreboard"
        onToolChange={onToolChange}
      />
    );

    const diceButtons = screen.getAllByRole('tab', { name: 'Dadi' });
    diceButtons[0].focus();
    await user.keyboard('{Enter}');
    expect(onToolChange).toHaveBeenCalledWith('dice');
  });

  it('short labels are rendered in mobile nav', () => {
    render(
      <ToolRail
        tools={BASE_TOOLS}
        activeTool="scoreboard"
        onToolChange={vi.fn()}
      />
    );

    // Short labels appear in the mobile nav
    expect(screen.getAllByText('Score').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Turno').length).toBeGreaterThan(0);
  });

  it('collapse toggle button is rendered with correct aria-label', () => {
    render(
      <ToolRail
        tools={BASE_TOOLS}
        activeTool="scoreboard"
        onToolChange={vi.fn()}
      />
    );

    expect(screen.getByRole('button', { name: 'Comprimi rail' })).toBeInTheDocument();
  });

  it('exports BASE_TOOLS with 5 base tool definitions', () => {
    expect(BASE_TOOLS).toHaveLength(5);
    expect(BASE_TOOLS.map(t => t.id)).toEqual(['scoreboard', 'turn-order', 'dice', 'whiteboard', 'camera']);
    BASE_TOOLS.forEach(t => expect(t.type).toBe('base'));
  });
});
