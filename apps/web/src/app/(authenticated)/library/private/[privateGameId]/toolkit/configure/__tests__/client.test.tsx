/**
 * UserToolkitConfiguratorClient tests (Issue #4980)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, within, fireEvent, waitFor } from '@testing-library/react';

import type { GameToolkitDto } from '@/lib/types/gameToolkit';

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn(), info: vi.fn() },
}));

// Stub UI primitives
vi.mock('@/components/ui/primitives/button', () => ({
  Button: ({
    children,
    onClick,
    disabled,
    type,
    ...rest
  }: React.ButtonHTMLAttributes<HTMLButtonElement> & { children?: React.ReactNode }) => (
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

// Mock usePrivateToolkitEditor
const mockEditor = {
  toolkit: null as GameToolkitDto | null,
  isLoading: false,
  isSaving: false,
  isAccessDenied: false,
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
};

vi.mock('@/lib/hooks/usePrivateToolkitEditor', () => ({
  usePrivateToolkitEditor: () => mockEditor,
}));

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeToolkit(overrides: Partial<GameToolkitDto> = {}): GameToolkitDto {
  return {
    id: 'toolkit-1',
    gameId: null,
    privateGameId: 'priv-abc',
    name: 'My Toolkit',
    version: 3,
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

import { UserToolkitConfiguratorClient } from '../client';

// ── Setup ─────────────────────────────────────────────────────────────────────

beforeEach(() => {
  vi.clearAllMocks();
  mockEditor.toolkit = null;
  mockEditor.isLoading = false;
  mockEditor.isSaving = false;
  mockEditor.isAccessDenied = false;
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
});

afterEach(() => {
  vi.restoreAllMocks();
});

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('UserToolkitConfiguratorClient — access denied', () => {
  it('shows access denied panel when isAccessDenied is true', () => {
    mockEditor.isAccessDenied = true;
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);
    expect(screen.getByText(/Accesso negato/i)).toBeInTheDocument();
    expect(screen.getByText(/Non sei il proprietario/i)).toBeInTheDocument();
  });

  it('shows back button to library in access denied panel', () => {
    mockEditor.isAccessDenied = true;
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);
    // The AccessDeniedPanel renders a button with "Torna ai miei giochi" text
    expect(screen.getByText('Torna ai miei giochi')).toBeInTheDocument();
  });

  it('still shows header when access denied', () => {
    mockEditor.isAccessDenied = true;
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);
    expect(screen.getByText(/Toolkit Configurator/i)).toBeInTheDocument();
  });
});

describe('UserToolkitConfiguratorClient — loading state', () => {
  it('shows loading spinner when isLoading is true', () => {
    mockEditor.isLoading = true;
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);
    // Loading spinner shown — no create panel or access denied
    expect(screen.queryByText(/Nessun toolkit configurato/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Accesso negato/i)).not.toBeInTheDocument();
  });
});

describe('UserToolkitConfiguratorClient — no toolkit state', () => {
  it('shows create panel when toolkit is null', () => {
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);
    expect(screen.getByText(/Nessun toolkit configurato/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /crea/i })).toBeInTheDocument();
  });

  it('create button is disabled when name is empty', () => {
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);
    const btn = screen.getByRole('button', { name: /crea/i });
    expect(btn).toBeDisabled();
  });

  it('calls createToolkit when form is submitted', async () => {
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);

    const input = screen.getByRole('textbox', { name: /nome toolkit/i });
    fireEvent.change(input, { target: { value: 'My Toolkit' } });
    fireEvent.click(screen.getByRole('button', { name: /crea/i }));

    await waitFor(() => {
      expect(mockEditor.createToolkit).toHaveBeenCalledWith('My Toolkit');
    });
  });
});

describe('UserToolkitConfiguratorClient — with toolkit', () => {
  beforeEach(() => {
    mockEditor.toolkit = makeToolkit();
  });

  it('shows toolkit configurator layout', () => {
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);
    expect(screen.getByText(/Toolkit Configurator/i)).toBeInTheDocument();
    expect(screen.getByText(/Override Tool Base/i)).toBeInTheDocument();
    expect(screen.getByText(/Tool Extra/i)).toBeInTheDocument();
  });

  it('shows override toggles for turn order, scoreboard, and dice', () => {
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);
    expect(screen.getByRole('switch', { name: /Ordine di turno/i })).toBeInTheDocument();
    expect(screen.getByRole('switch', { name: /Scoreboard/i })).toBeInTheDocument();
    expect(screen.getByRole('switch', { name: /Set dadi/i })).toBeInTheDocument();
  });

  it('calls updateOverrides when a toggle is flipped', async () => {
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);

    const turnOrderSwitch = screen.getByRole('switch', { name: /Ordine di turno/i });
    fireEvent.click(turnOrderSwitch);

    await waitFor(() => {
      expect(mockEditor.updateOverrides).toHaveBeenCalledWith({ overridesTurnOrder: true });
    });
  });

  it('does NOT show a Publish button', () => {
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);
    expect(screen.queryByRole('button', { name: /pubblica/i })).not.toBeInTheDocument();
  });

  it('shows "Salvataggio…" text when isSaving is true', () => {
    mockEditor.isSaving = true;
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);
    expect(screen.getByText(/Salvataggio/i)).toBeInTheDocument();
  });

  it('shows version badge in header', () => {
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);
    expect(screen.getByText(/v3/i)).toBeInTheDocument();
  });

  it('shows preview section', () => {
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);
    const preview = screen.getByLabelText(/Anteprima tool rail/i);
    expect(preview).toBeInTheDocument();
    expect(preview).toHaveTextContent('Scoreboard');
  });

  it('shows error banner when error is set', () => {
    mockEditor.error = 'HTTP 500: Internal Server Error';
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);
    expect(screen.getByRole('alert')).toHaveTextContent('HTTP 500');
  });
});

describe('UserToolkitConfiguratorClient — extra tool forms', () => {
  beforeEach(() => {
    mockEditor.toolkit = makeToolkit();
  });

  it('shows dice form when Aggiungi dado is clicked', () => {
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);
    const addButtons = screen.getAllByRole('button', { name: /aggiungi/i });
    fireEvent.click(addButtons[0]); // First "Aggiungi" is for dice
    expect(screen.getByRole('button', { name: /annulla/i })).toBeInTheDocument();
  });

  it('hides dice form when Annulla is clicked', () => {
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);
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
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);
    // Scope to the Tool extra section to avoid matching the preview panel
    const toolExtra = screen.getByRole('region', { name: /Tool extra/i });
    expect(within(toolExtra).getByText('2×d6')).toBeInTheDocument();
  });

  it('calls removeDiceTool when trash icon is clicked', async () => {
    mockEditor.toolkit = makeToolkit({
      diceTools: [{ name: '2×d6', diceType: 'd6', quantity: 2, isInteractive: true }],
    });
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);

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
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);
    // Scope to the Tool extra section to avoid matching the preview panel
    const toolExtra = screen.getByRole('region', { name: /Tool extra/i });
    expect(within(toolExtra).getByText('Risorse')).toBeInTheDocument();
  });

  it('calls removeCounterTool when trash icon is clicked', async () => {
    mockEditor.toolkit = makeToolkit({
      counterTools: [
        { name: 'Risorse', minValue: 0, maxValue: 100, defaultValue: 0, isPerPlayer: true },
      ],
    });
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);

    const removeBtn = screen.getByRole('button', { name: /rimuovi Risorse/i });
    fireEvent.click(removeBtn);

    await waitFor(() => {
      expect(mockEditor.removeCounterTool).toHaveBeenCalledWith('Risorse');
    });
  });
});

describe('UserToolkitConfiguratorClient — preview', () => {
  it('hides dice from preview when overridesDiceSet is true (unpublished toolkit)', () => {
    // Private-game toolkits are never published; the preview treats them as
    // if published so override flags are respected.
    mockEditor.toolkit = makeToolkit({ overridesDiceSet: true });
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);

    const preview = screen.getByLabelText(/Anteprima tool rail/i);
    expect(preview).not.toHaveTextContent('Dadi');
  });

  it('shows custom tools in preview when diceTools are added', () => {
    mockEditor.toolkit = makeToolkit({
      overridesDiceSet: true,
      diceTools: [{ name: '4×d4', diceType: 'd4', quantity: 4, isInteractive: true }],
    });
    render(<UserToolkitConfiguratorClient privateGameId="priv-abc" />);

    const preview = screen.getByLabelText(/Anteprima tool rail/i);
    expect(preview).toHaveTextContent('4×d4');
  });
});
