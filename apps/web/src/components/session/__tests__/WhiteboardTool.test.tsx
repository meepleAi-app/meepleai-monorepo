/**
 * WhiteboardTool Component Tests (Issue #4977)
 *
 * Coverage:
 * - Loading state: aria-busy shown when whiteboardState is null
 * - Mode switcher: buttons change active mode
 * - Error display: error prop renders role="alert"
 * - Freehand mode: canvas rendered, color palette, thickness, eraser
 * - Structured mode: grid cells rendered, add token panel
 * - Both mode (default): canvas + structured layers both present
 * - Token management: add, remove, drag between cells
 * - Grid controls: toggle, size change
 * - Clear all: confirm dialog, confirm / cancel
 * - isPending: save indicator shown, buttons disabled
 * - Accessibility: aria-pressed, aria-label, aria-live
 */

import React from 'react';

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { WhiteboardTool } from '../WhiteboardTool';
import type { WhiteboardState, WhiteboardToken } from '../types';

// ── Canvas mock ───────────────────────────────────────────────────────────────

const mockCtx = {
  clearRect: vi.fn(),
  beginPath: vi.fn(),
  moveTo: vi.fn(),
  lineTo: vi.fn(),
  stroke: vi.fn(),
  save: vi.fn(),
  restore: vi.fn(),
  setLineDash: vi.fn(),
  fillRect: vi.fn(),
  strokeRect: vi.fn(),
  fillStyle: '',
  strokeStyle: '',
  lineWidth: 0,
  lineCap: '' as CanvasLineCap,
  lineJoin: '' as CanvasLineJoin,
  globalCompositeOperation: '' as GlobalCompositeOperation,
  globalAlpha: 1,
};

beforeEach(() => {
  vi.clearAllMocks();
  // Mock canvas context for all tests
  HTMLCanvasElement.prototype.getContext = vi.fn().mockReturnValue(mockCtx);
  // Mock getBoundingClientRect for the canvas container
  HTMLElement.prototype.getBoundingClientRect = vi.fn().mockReturnValue({
    left: 0, top: 0, right: 400, bottom: 300,
    width: 400, height: 300, x: 0, y: 0, toJSON: () => ({}),
  });
});

// ── Fixtures ──────────────────────────────────────────────────────────────────

const EMPTY_STATE: WhiteboardState = {
  strokes: [],
  tokens: [],
  gridSize: '4x4',
  showGrid: true,
  mode: 'both',
};

const STATE_WITH_TOKENS: WhiteboardState = {
  ...EMPTY_STATE,
  tokens: [
    { id: 'tok-1', color: '#ef4444', label: 'A', gridX: 0, gridY: 0 },
    { id: 'tok-2', color: '#22c55e', label: 'B', gridX: 1, gridY: 1 },
  ],
};

// ── Loading state ─────────────────────────────────────────────────────────────

describe('loading state', () => {
  it('shows aria-busy container when whiteboardState is null', () => {
    render(
      <WhiteboardTool
        whiteboardState={null}
        onStrokesChange={vi.fn()}
      />
    );
    expect(document.querySelector('[aria-busy="true"]')).not.toBeNull();
  });

  it('shows sr-only loading text when whiteboardState is null', () => {
    render(
      <WhiteboardTool
        whiteboardState={null}
        onStrokesChange={vi.fn()}
      />
    );
    expect(document.querySelector('.sr-only')?.textContent).toBe('Caricamento lavagna…');
  });

  it('renders whiteboard when state is provided', () => {
    render(
      <WhiteboardTool
        whiteboardState={EMPTY_STATE}
        onStrokesChange={vi.fn()}
      />
    );
    expect(screen.getByRole('region', { hidden: true })).toBeInTheDocument();
  });
});

// ── Mode switcher ─────────────────────────────────────────────────────────────

describe('mode switcher', () => {
  it('renders all three mode buttons', () => {
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    expect(screen.getByRole('button', { name: 'Solo disegno' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Solo griglia' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Entrambi' })).toBeInTheDocument();
  });

  it('"Entrambi" is pressed by default', () => {
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    expect(screen.getByRole('button', { name: 'Entrambi' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('switches to freehand mode on click', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    await user.click(screen.getByRole('button', { name: 'Solo disegno' }));
    expect(screen.getByRole('button', { name: 'Solo disegno' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'Entrambi' })).toHaveAttribute('aria-pressed', 'false');
  });

  it('switches to structured mode on click', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    await user.click(screen.getByRole('button', { name: 'Solo griglia' }));
    expect(screen.getByRole('button', { name: 'Solo griglia' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('calls onStructuredChange when mode changes', async () => {
    const onStructuredChange = vi.fn();
    const user = userEvent.setup();
    render(
      <WhiteboardTool
        whiteboardState={EMPTY_STATE}
        onStructuredChange={onStructuredChange}
      />
    );
    await user.click(screen.getByRole('button', { name: 'Solo griglia' }));
    expect(onStructuredChange).toHaveBeenCalledWith(
      expect.any(Array), // tokens
      expect.any(String), // gridSize
      expect.any(Boolean), // showGrid
      'structured'
    );
  });
});

// ── Canvas / freehand mode ────────────────────────────────────────────────────

describe('freehand mode', () => {
  it('renders canvas element in both mode (default)', () => {
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    expect(screen.getByTestId('whiteboard-canvas')).toBeInTheDocument();
  });

  it('hides canvas in structured-only mode', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    await user.click(screen.getByRole('button', { name: 'Solo griglia' }));
    expect(screen.queryByTestId('whiteboard-canvas')).toBeNull();
  });

  it('renders color palette buttons (9 colors)', () => {
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    const colorGroup = screen.getByRole('group', { name: 'Palette colori' });
    const colorBtns = within(colorGroup).getAllByRole('button');
    expect(colorBtns).toHaveLength(9);
  });

  it('selects a color on click', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    const colorGroup = screen.getByRole('group', { name: 'Palette colori' });
    const redBtn = within(colorGroup).getByRole('button', { name: 'Colore #ef4444' });
    await user.click(redBtn);
    expect(redBtn).toHaveAttribute('aria-pressed', 'true');
  });

  it('renders 3 thickness preset buttons', () => {
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    const thicknessGroup = screen.getByRole('group', { name: 'Spessore pennello' });
    expect(within(thicknessGroup).getAllByRole('button')).toHaveLength(3);
  });

  it('selects thickness on click', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    const thicknessGroup = screen.getByRole('group', { name: 'Spessore pennello' });
    const thinBtn = within(thicknessGroup).getByRole('button', { name: 'Sottile' });
    await user.click(thinBtn);
    expect(thinBtn).toHaveAttribute('aria-pressed', 'true');
  });

  it('renders eraser button', () => {
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    expect(screen.getByRole('button', { name: 'Gomma' })).toBeInTheDocument();
  });

  it('toggles eraser on/off', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    const eraserBtn = screen.getByRole('button', { name: 'Gomma' });
    expect(eraserBtn).toHaveAttribute('aria-pressed', 'false');
    await user.click(eraserBtn);
    expect(eraserBtn).toHaveAttribute('aria-pressed', 'true');
    await user.click(eraserBtn);
    expect(eraserBtn).toHaveAttribute('aria-pressed', 'false');
  });

  it('deactivates eraser when a color is selected', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    // Activate eraser
    await user.click(screen.getByRole('button', { name: 'Gomma' }));
    expect(screen.getByRole('button', { name: 'Gomma' })).toHaveAttribute('aria-pressed', 'true');
    // Click a color
    const colorGroup = screen.getByRole('group', { name: 'Palette colori' });
    await user.click(within(colorGroup).getByRole('button', { name: 'Colore #000000' }));
    // Eraser should be off
    expect(screen.getByRole('button', { name: 'Gomma' })).toHaveAttribute('aria-pressed', 'false');
  });
});

// ── Structured layer ──────────────────────────────────────────────────────────

describe('structured layer', () => {
  it('renders structured layer in both mode (default)', () => {
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    expect(screen.getByTestId('whiteboard-structured')).toBeInTheDocument();
  });

  it('hides structured layer in freehand-only mode', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    await user.click(screen.getByRole('button', { name: 'Solo disegno' }));
    expect(screen.queryByTestId('whiteboard-structured')).toBeNull();
  });

  it('renders 4×4 = 16 grid cells by default', () => {
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    expect(screen.getAllByTestId(/^grid-cell-/)).toHaveLength(16);
  });

  it('renders tokens in the correct cells', () => {
    render(<WhiteboardTool whiteboardState={STATE_WITH_TOKENS} />);
    expect(screen.getByRole('img', { name: 'Token A' })).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Token B' })).toBeInTheDocument();
  });

  it('removes token when remove button is clicked', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={STATE_WITH_TOKENS} />);
    // Focus/hover to make remove button visible, then click
    const removeBtn = screen.getByRole('button', { name: 'Rimuovi token A' });
    await user.click(removeBtn);
    expect(screen.queryByRole('img', { name: 'Token A' })).toBeNull();
    expect(screen.getByRole('img', { name: 'Token B' })).toBeInTheDocument();
  });

  it('calls onStructuredChange when token is removed', async () => {
    const user = userEvent.setup();
    const onStructuredChange = vi.fn();
    render(
      <WhiteboardTool
        whiteboardState={STATE_WITH_TOKENS}
        onStructuredChange={onStructuredChange}
      />
    );
    await user.click(screen.getByRole('button', { name: 'Rimuovi token A' }));
    expect(onStructuredChange).toHaveBeenCalledWith(
      expect.arrayContaining([expect.objectContaining({ id: 'tok-2' })]),
      expect.any(String),
      expect.any(Boolean),
      expect.any(String)
    );
    // tok-1 should NOT be in the call
    const [[tokens]] = onStructuredChange.mock.calls;
    expect((tokens as WhiteboardToken[]).find(t => t.id === 'tok-1')).toBeUndefined();
  });
});

// ── Grid controls ─────────────────────────────────────────────────────────────

describe('grid controls', () => {
  it('renders grid toggle button', () => {
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    expect(screen.getByRole('button', { name: 'Nascondi griglia' })).toBeInTheDocument();
  });

  it('toggles grid aria-pressed state', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    const toggleBtn = screen.getByRole('button', { name: 'Nascondi griglia' });
    expect(toggleBtn).toHaveAttribute('aria-pressed', 'true');
    await user.click(toggleBtn);
    expect(screen.getByRole('button', { name: 'Mostra griglia' })).toHaveAttribute('aria-pressed', 'false');
  });

  it('renders grid size selector with 3 options', () => {
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    const select = screen.getByRole('combobox', { name: 'Dimensione griglia' });
    expect(select).toBeInTheDocument();
    expect(within(select).getAllByRole('option')).toHaveLength(3);
  });

  it('changes grid size to 6×6 and renders 36 cells', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    const select = screen.getByRole('combobox', { name: 'Dimensione griglia' });
    await user.selectOptions(select, '6x6');
    expect(screen.getAllByTestId(/^grid-cell-/)).toHaveLength(36);
  });

  it('changes grid size to 8×8 and renders 64 cells', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    const select = screen.getByRole('combobox', { name: 'Dimensione griglia' });
    await user.selectOptions(select, '8x8');
    expect(screen.getAllByTestId(/^grid-cell-/)).toHaveLength(64);
  });

  it('calls onStructuredChange when grid size changes', async () => {
    const user = userEvent.setup();
    const onStructuredChange = vi.fn();
    render(
      <WhiteboardTool
        whiteboardState={EMPTY_STATE}
        onStructuredChange={onStructuredChange}
      />
    );
    await user.selectOptions(
      screen.getByRole('combobox', { name: 'Dimensione griglia' }),
      '6x6'
    );
    expect(onStructuredChange).toHaveBeenCalledWith(
      expect.any(Array),
      '6x6',
      expect.any(Boolean),
      expect.any(String)
    );
  });
});

// ── Add token panel ───────────────────────────────────────────────────────────

describe('add token panel', () => {
  it('shows add token panel when "Aggiungi token" is clicked', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    await user.click(screen.getByRole('button', { name: 'Aggiungi token' }));
    expect(screen.getByTestId('add-token-panel')).toBeInTheDocument();
  });

  it('hides add token panel when "Annulla" is clicked', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    await user.click(screen.getByRole('button', { name: 'Aggiungi token' }));
    await user.click(screen.getByRole('button', { name: 'Annulla aggiunta token' }));
    expect(screen.queryByTestId('add-token-panel')).toBeNull();
  });

  it('adds a token when label is provided and Aggiungi is clicked', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    await user.click(screen.getByRole('button', { name: 'Aggiungi token' }));
    await user.type(screen.getByRole('textbox', { name: 'Etichetta token' }), 'Z');
    await user.click(screen.getByRole('button', { name: 'Conferma aggiunta token' }));
    expect(screen.getByRole('img', { name: 'Token Z' })).toBeInTheDocument();
    expect(screen.queryByTestId('add-token-panel')).toBeNull();
  });

  it('"Aggiungi" button is disabled when label is empty', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    await user.click(screen.getByRole('button', { name: 'Aggiungi token' }));
    expect(screen.getByRole('button', { name: 'Conferma aggiunta token' })).toBeDisabled();
  });

  it('renders 8 token color options', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    await user.click(screen.getByRole('button', { name: 'Aggiungi token' }));
    const colorGroup = screen.getByRole('group', { name: 'Colore token' });
    expect(within(colorGroup).getAllByRole('button')).toHaveLength(8);
  });
});

// ── Clear all ─────────────────────────────────────────────────────────────────

describe('clear all', () => {
  it('shows confirm dialog when "Cancella" is clicked', async () => {
    const user = userEvent.setup();
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    await user.click(screen.getByRole('button', { name: 'Cancella lavagna' }));
    expect(screen.getByTestId('clear-confirm-dialog')).toBeInTheDocument();
    expect(screen.getByRole('alertdialog')).toBeInTheDocument();
  });

  it('closes dialog without clearing when "Annulla" is clicked', async () => {
    const user = userEvent.setup();
    const onClear = vi.fn();
    render(<WhiteboardTool whiteboardState={STATE_WITH_TOKENS} onClear={onClear} />);
    await user.click(screen.getByRole('button', { name: 'Cancella lavagna' }));
    await user.click(screen.getByRole('button', { name: 'Annulla cancellazione' }));
    expect(screen.queryByTestId('clear-confirm-dialog')).toBeNull();
    expect(onClear).not.toHaveBeenCalled();
    // Tokens still visible
    expect(screen.getByRole('img', { name: 'Token A' })).toBeInTheDocument();
  });

  it('calls onClear and removes tokens when confirmed', async () => {
    const user = userEvent.setup();
    const onClear = vi.fn().mockResolvedValue(undefined);
    render(<WhiteboardTool whiteboardState={STATE_WITH_TOKENS} onClear={onClear} />);
    await user.click(screen.getByRole('button', { name: 'Cancella lavagna' }));
    await user.click(screen.getByRole('button', { name: 'Conferma cancellazione lavagna' }));
    await waitFor(() => expect(onClear).toHaveBeenCalled());
    expect(screen.queryByRole('img', { name: 'Token A' })).toBeNull();
  });
});

// ── Error state ───────────────────────────────────────────────────────────────

it('shows error message when error prop is set', () => {
  render(
    <WhiteboardTool
      whiteboardState={EMPTY_STATE}
      error="Connection failed"
    />
  );
  expect(screen.getByRole('alert')).toBeInTheDocument();
  expect(screen.getByText('Connection failed')).toBeInTheDocument();
});

// ── isPending ─────────────────────────────────────────────────────────────────

it('shows saving indicator when isPending is true', () => {
  render(<WhiteboardTool whiteboardState={EMPTY_STATE} isPending />);
  expect(screen.getByText('Salvataggio…')).toBeInTheDocument();
});

it('disables color buttons when isPending', () => {
  render(<WhiteboardTool whiteboardState={EMPTY_STATE} isPending />);
  const colorGroup = screen.getByRole('group', { name: 'Palette colori' });
  within(colorGroup).getAllByRole('button').forEach(btn => {
    expect(btn).toBeDisabled();
  });
});

// ── Custom className ──────────────────────────────────────────────────────────

it('applies custom className to section', () => {
  const { container } = render(
    <WhiteboardTool whiteboardState={EMPTY_STATE} className="custom-wb" />
  );
  expect(container.querySelector('.custom-wb')).not.toBeNull();
});

// ── Accessibility ─────────────────────────────────────────────────────────────

describe('accessibility', () => {
  it('renders section with aria-labelledby pointing to h2', () => {
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    const section = document.querySelector('section[aria-labelledby]');
    expect(section).not.toBeNull();
    const labelId = section!.getAttribute('aria-labelledby')!;
    const heading = document.getElementById(labelId);
    expect(heading?.tagName).toBe('H2');
    expect(heading?.textContent).toContain('Lavagna');
  });

  it('mode buttons have aria-pressed attribute', () => {
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    ['Solo disegno', 'Solo griglia', 'Entrambi'].forEach(name => {
      const btn = screen.getByRole('button', { name });
      expect(btn).toHaveAttribute('aria-pressed');
    });
  });

  it('canvas has aria-label', () => {
    render(<WhiteboardTool whiteboardState={EMPTY_STATE} />);
    const canvas = screen.getByTestId('whiteboard-canvas');
    expect(canvas).toHaveAttribute('aria-label', 'Lavagna freehand');
  });
});
