/**
 * ToolkitConfiguratorClient tests (Issue #4978)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

import type { GameToolkitDto } from '@/lib/types/gameToolkit';

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn(), info: vi.fn() },
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({ user: { role: 'Admin', email: 'admin@test.com' }, loading: false }),
}));

vi.mock('@/components/admin/AdminAuthGuard', () => ({
  AdminAuthGuard: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));

// Stub UI primitives
vi.mock('@/components/ui/primitives/button', () => ({
  Button: ({ children, onClick, disabled, type, ...rest }: React.ButtonHTMLAttributes<HTMLButtonElement> & { children?: React.ReactNode }) => (
    <button onClick={onClick} disabled={disabled} type={type ?? 'button'} {...rest}>
      {children}
    </button>
  ),
}));

vi.mock('@/components/ui/primitives/input', () => ({
  Input: (props: React.InputHTMLAttributes<HTMLInputElement>) => <input {...props} />,
}));

vi.mock('@/components/ui/primitives/label', () => ({
  Label: ({ children, ...props }: React.LabelHTMLAttributes<HTMLLabelElement>) => (
    <label {...props}>{children}</label>
  ),
}));

vi.mock('@/components/ui/forms/switch', () => ({
  Switch: ({
    checked,
    onCheckedChange,
    disabled,
    id,
    'aria-label': ariaLabel,
  }: {
    checked?: boolean;
    onCheckedChange?: (v: boolean) => void;
    disabled?: boolean;
    id?: string;
    'aria-label'?: string;
  }) => (
    <input
      type="checkbox"
      checked={checked}
      onChange={e => onCheckedChange?.(e.target.checked)}
      disabled={disabled}
      id={id}
      role="switch"
      aria-label={ariaLabel}
    />
  ),
}));

// Mock useToolkitEditor
const mockEditor = {
  toolkit: null as GameToolkitDto | null,
  isLoading: false,
  isSaving: false,
  error: null as string | null,
  createToolkit: vi.fn(),
  updateOverrides: vi.fn(),
  addDiceTool: vi.fn(),
  removeDiceTool: vi.fn(),
  addCardTool: vi.fn(),
  removeCardTool: vi.fn(),
  addTimerTool: vi.fn(),
  removeTimerTool: vi.fn(),
  addCounterTool: vi.fn(),
  removeCounterTool: vi.fn(),
  publish: vi.fn(),
};

vi.mock('@/lib/hooks/useToolkitEditor', () => ({
  useToolkitEditor: () => mockEditor,
}));

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeToolkit(overrides: Partial<GameToolkitDto> = {}): GameToolkitDto {
  return {
    id: 'toolkit-1',
    gameId: 'game-abc',
    privateGameId: null,
    name: 'Catan Toolkit',
    version: 2,
    isPublished: false,
    overridesTurnOrder: false,
    overridesScoreboard: false,
    overridesDiceSet: false,
    diceTools: [],
    cardTools: [],
    timerTools: [],
    counterTools: [],
    ...overrides,
  };
}

import { ToolkitConfiguratorClient } from '../client';

// ── Setup ─────────────────────────────────────────────────────────────────────

beforeEach(() => {
  vi.clearAllMocks();
  mockEditor.toolkit = null;
  mockEditor.isLoading = false;
  mockEditor.isSaving = false;
  mockEditor.error = null;
  mockEditor.createToolkit.mockResolvedValue(undefined);
  mockEditor.updateOverrides.mockResolvedValue(undefined);
  mockEditor.addDiceTool.mockResolvedValue(undefined);
  mockEditor.removeDiceTool.mockResolvedValue(undefined);
  mockEditor.addCardTool.mockResolvedValue(undefined);
  mockEditor.removeCardTool.mockResolvedValue(undefined);
  mockEditor.addTimerTool.mockResolvedValue(undefined);
  mockEditor.removeTimerTool.mockResolvedValue(undefined);
  mockEditor.addCounterTool.mockResolvedValue(undefined);
  mockEditor.removeCounterTool.mockResolvedValue(undefined);
  mockEditor.publish.mockResolvedValue(undefined);
});

afterEach(() => {
  vi.restoreAllMocks();
});

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('ToolkitConfiguratorClient — no toolkit state', () => {
  it('shows create panel when toolkit is null', () => {
    render(<ToolkitConfiguratorClient gameId="game-abc" />);
    expect(screen.getByText(/Nessun toolkit configurato/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /crea/i })).toBeInTheDocument();
  });

  it('shows loading spinner when isLoading is true', () => {
    mockEditor.isLoading = true;
    render(<ToolkitConfiguratorClient gameId="game-abc" />);
    // Loading spinner is present — no create panel
    expect(screen.queryByText(/Nessun toolkit configurato/i)).not.toBeInTheDocument();
  });

  it('calls createToolkit when form is submitted', async () => {
    render(<ToolkitConfiguratorClient gameId="game-abc" />);

    const input = screen.getByRole('textbox', { name: /nome toolkit/i });
    fireEvent.change(input, { target: { value: 'My Toolkit' } });
    fireEvent.click(screen.getByRole('button', { name: /crea/i }));

    await waitFor(() => {
      expect(mockEditor.createToolkit).toHaveBeenCalledWith('My Toolkit');
    });
  });

  it('create button is disabled when name is empty', () => {
    render(<ToolkitConfiguratorClient gameId="game-abc" />);
    const btn = screen.getByRole('button', { name: /crea/i });
    expect(btn).toBeDisabled();
  });
});

describe('ToolkitConfiguratorClient — with toolkit', () => {
  beforeEach(() => {
    mockEditor.toolkit = makeToolkit();
  });

  it('shows toolkit configurator layout', () => {
    render(<ToolkitConfiguratorClient gameId="game-abc" />);
    expect(screen.getByText(/Toolkit Configurator/i)).toBeInTheDocument();
    expect(screen.getByText(/Override Tool Base/i)).toBeInTheDocument();
    expect(screen.getByText(/Tool Extra/i)).toBeInTheDocument();
  });

  it('shows override toggles for turn order, scoreboard, and dice', () => {
    render(<ToolkitConfiguratorClient gameId="game-abc" />);
    expect(screen.getByRole('switch', { name: /Ordine di turno/i })).toBeInTheDocument();
    expect(screen.getByRole('switch', { name: /Scoreboard/i })).toBeInTheDocument();
    expect(screen.getByRole('switch', { name: /Set dadi/i })).toBeInTheDocument();
  });

  it('calls updateOverrides when a toggle is flipped', async () => {
    render(<ToolkitConfiguratorClient gameId="game-abc" />);

    const turnOrderSwitch = screen.getByRole('switch', { name: /Ordine di turno/i });
    fireEvent.click(turnOrderSwitch);

    await waitFor(() => {
      expect(mockEditor.updateOverrides).toHaveBeenCalledWith({ overridesTurnOrder: true });
    });
  });

  it('shows publish button with version', () => {
    render(<ToolkitConfiguratorClient gameId="game-abc" />);
    expect(screen.getByRole('button', { name: /pubblica v3/i })).toBeInTheDocument();
  });

  it('calls publish when publish button is clicked', async () => {
    render(<ToolkitConfiguratorClient gameId="game-abc" />);
    fireEvent.click(screen.getByRole('button', { name: /pubblica v3/i }));
    await waitFor(() => {
      expect(mockEditor.publish).toHaveBeenCalled();
    });
  });

  it('shows preview section with base tools', () => {
    render(<ToolkitConfiguratorClient gameId="game-abc" />);
    const preview = screen.getByLabelText(/Anteprima tool rail/i);
    expect(preview).toBeInTheDocument();
    // All 4 base tools visible when no overrides (isPublished: false → all base tools shown)
    expect(preview).toHaveTextContent('Scoreboard');
  });

  it('shows error banner when error is set', () => {
    mockEditor.error = 'HTTP 500: Internal Server Error';
    render(<ToolkitConfiguratorClient gameId="game-abc" />);
    expect(screen.getByRole('alert')).toHaveTextContent('HTTP 500');
  });
});

describe('ToolkitConfiguratorClient — extra tool forms', () => {
  beforeEach(() => {
    mockEditor.toolkit = makeToolkit();
  });

  it('shows dice form when Aggiungi dado is clicked', () => {
    render(<ToolkitConfiguratorClient gameId="game-abc" />);
    // Click the first "Aggiungi" button (dice section)
    const addButtons = screen.getAllByRole('button', { name: /aggiungi/i });
    fireEvent.click(addButtons[0]); // First "Aggiungi" is for dice

    expect(screen.getByRole('button', { name: /annulla/i })).toBeInTheDocument();
  });

  it('hides dice form when Annulla is clicked', () => {
    render(<ToolkitConfiguratorClient gameId="game-abc" />);
    const addButtons = screen.getAllByRole('button', { name: /aggiungi/i });
    fireEvent.click(addButtons[0]);

    const annullaBtn = screen.getByRole('button', { name: /annulla/i });
    fireEvent.click(annullaBtn);

    expect(screen.queryByRole('button', { name: /annulla/i })).not.toBeInTheDocument();
  });

  it('shows existing dice tools in list', () => {
    mockEditor.toolkit = makeToolkit({
      diceTools: [{ name: '2×d6', diceType: 'd6', quantity: 2, isInteractive: true }],
    });
    render(<ToolkitConfiguratorClient gameId="game-abc" />);
    expect(screen.getByText('2×d6')).toBeInTheDocument();
  });

  it('calls removeDiceTool when trash icon is clicked', async () => {
    mockEditor.toolkit = makeToolkit({
      diceTools: [{ name: '2×d6', diceType: 'd6', quantity: 2, isInteractive: true }],
    });
    render(<ToolkitConfiguratorClient gameId="game-abc" />);

    const removeBtn = screen.getByRole('button', { name: /rimuovi 2×d6/i });
    fireEvent.click(removeBtn);

    await waitFor(() => {
      expect(mockEditor.removeDiceTool).toHaveBeenCalledWith('2×d6');
    });
  });

  it('shows existing counter tools in list', () => {
    mockEditor.toolkit = makeToolkit({
      counterTools: [
        { name: 'Risorse', minValue: 0, maxValue: 100, defaultValue: 0, isPerPlayer: true },
      ],
    });
    render(<ToolkitConfiguratorClient gameId="game-abc" />);
    expect(screen.getByText('Risorse')).toBeInTheDocument();
  });
});

describe('ToolkitConfiguratorClient — preview', () => {
  it('hides dice from preview when overridesDiceSet is true and toolkit is published', () => {
    // resolveSessionTools only applies overrides when isPublished=true
    mockEditor.toolkit = makeToolkit({ overridesDiceSet: true, isPublished: true });
    render(<ToolkitConfiguratorClient gameId="game-abc" />);

    const preview = screen.getByLabelText(/Anteprima tool rail/i);
    // Dice base tool should not appear; only scoreboard, turn-order, whiteboard
    expect(preview).not.toHaveTextContent('Dadi');
  });

  it('shows custom tools in preview when diceTools are added (published toolkit)', () => {
    // resolveSessionTools only applies overrides when isPublished=true
    mockEditor.toolkit = makeToolkit({
      overridesDiceSet: true,
      isPublished: true,
      diceTools: [{ name: '4×d4', diceType: 'd4', quantity: 4, isInteractive: true }],
    });
    render(<ToolkitConfiguratorClient gameId="game-abc" />);

    const preview = screen.getByLabelText(/Anteprima tool rail/i);
    expect(preview).toHaveTextContent('4×d4');
  });
});
