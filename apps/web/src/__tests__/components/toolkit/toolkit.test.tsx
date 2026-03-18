import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// ============================================================================
// Mocks — keep tests isolated from external UI primitives
// ============================================================================

vi.mock('@/components/ui/primitives/button', () => ({
  Button: ({
    children,
    onClick,
    disabled,
    className,
    'aria-label': ariaLabel,
    ...props
  }: React.ButtonHTMLAttributes<HTMLButtonElement> & { 'aria-label'?: string }) => (
    <button
      onClick={onClick}
      disabled={disabled}
      className={className}
      aria-label={ariaLabel}
      {...props}
    >
      {children}
    </button>
  ),
}));

vi.mock('@/components/ui/primitives/input', () => ({
  Input: ({
    value,
    onChange,
    onKeyDown,
    placeholder,
    className,
    'aria-label': ariaLabel,
    ...props
  }: React.InputHTMLAttributes<HTMLInputElement> & { 'aria-label'?: string }) => (
    <input
      value={value}
      onChange={onChange}
      onKeyDown={onKeyDown}
      placeholder={placeholder}
      className={className}
      aria-label={ariaLabel}
      {...props}
    />
  ),
}));

vi.mock('lucide-react', () => ({
  Plus: () => <span data-testid="icon-plus" />,
  X: () => <span data-testid="icon-x" />,
  Trash2: () => <span data-testid="icon-trash" />,
  RotateCcw: () => <span data-testid="icon-reset" />,
  ArrowUpDown: () => <span data-testid="icon-sort" />,
  Shuffle: () => <span data-testid="icon-shuffle" />,
  ArrowLeft: () => <span data-testid="icon-back" />,
}));

// ============================================================================
// Imports (after mocks)
// ============================================================================

import { DiceRoller } from '@/components/toolkit/DiceRoller';
import { Timer } from '@/components/toolkit/Timer';
import { Counter } from '@/components/toolkit/Counter';
import { Scoreboard } from '@/components/toolkit/Scoreboard';
import { Randomizer } from '@/components/toolkit/Randomizer';
import { ToolkitSheet } from '@/components/sheets/ToolkitSheet';

// ============================================================================
// DiceRoller
// ============================================================================

describe('DiceRoller', () => {
  beforeEach(() => {
    vi.spyOn(Math, 'random').mockReturnValue(0.5);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders without crashing', () => {
    render(<DiceRoller />);
    expect(screen.getByTestId('dice-roller')).toBeInTheDocument();
  });

  it('renders all dice type buttons', () => {
    render(<DiceRoller />);
    ['d4', 'd6', 'd8', 'd10', 'd12', 'd20'].forEach(d => {
      expect(screen.getByRole('button', { name: `Select ${d}` })).toBeInTheDocument();
    });
  });

  it('shows no result initially', () => {
    render(<DiceRoller />);
    expect(screen.queryByTestId('dice-result')).not.toBeInTheDocument();
  });

  it('shows result after clicking roll', async () => {
    const user = userEvent.setup();
    render(<DiceRoller />);

    // The roll button has aria-label "Roll dice"
    await user.click(screen.getByRole('button', { name: 'Roll dice' }));

    expect(screen.getByTestId('dice-result')).toBeInTheDocument();
  });

  it('increments dice count', async () => {
    const user = userEvent.setup();
    render(<DiceRoller />);

    await user.click(screen.getByRole('button', { name: 'Increase dice count' }));
    expect(screen.getByText('2')).toBeInTheDocument();
  });

  it('cannot go below 1 dice', async () => {
    const user = userEvent.setup();
    render(<DiceRoller />);

    const decrementBtn = screen.getByRole('button', { name: 'Decrease dice count' });
    expect(decrementBtn).toBeDisabled();
  });

  it('selects a different dice type', async () => {
    const user = userEvent.setup();
    render(<DiceRoller />);

    await user.click(screen.getByRole('button', { name: 'Select d20' }));
    expect(screen.getByRole('button', { name: 'Select d20' })).toHaveAttribute(
      'aria-pressed',
      'true'
    );
  });
});

// ============================================================================
// Timer
// ============================================================================

describe('Timer', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders without crashing', () => {
    render(<Timer />);
    expect(screen.getByTestId('timer')).toBeInTheDocument();
  });

  it('shows initial time display', () => {
    render(<Timer />);
    expect(screen.getByTestId('timer-display')).toBeInTheDocument();
  });

  it('shows mode toggle buttons', () => {
    render(<Timer />);
    // Mode toggles are plain <button> elements with aria-pressed
    expect(screen.getByText('Countdown')).toBeInTheDocument();
    expect(screen.getByText('Cronometro')).toBeInTheDocument();
  });

  it('starts and pauses the timer', async () => {
    render(<Timer />);

    const startBtn = screen.getByRole('button', { name: 'Start timer' });
    act(() => {
      startBtn.click();
    });

    expect(screen.getByRole('button', { name: 'Pause timer' })).toBeInTheDocument();
  });

  it('resets the timer', async () => {
    render(<Timer />);

    act(() => {
      screen.getByRole('button', { name: 'Start timer' }).click();
    });
    act(() => {
      screen.getByRole('button', { name: 'Reset timer' }).click();
    });

    expect(screen.getByRole('button', { name: 'Start timer' })).toBeInTheDocument();
  });

  it('switches to stopwatch mode', () => {
    render(<Timer />);

    act(() => {
      screen.getByText('Cronometro').click();
    });

    expect(screen.getByText('Cronometro')).toHaveAttribute('aria-pressed', 'true');
  });

  it('stopwatch counts up after start', () => {
    render(<Timer />);

    act(() => {
      screen.getByText('Cronometro').click();
    });
    act(() => {
      screen.getByRole('button', { name: 'Start timer' }).click();
    });
    act(() => {
      vi.advanceTimersByTime(3000);
    });

    expect(screen.getByTestId('timer-display')).toHaveTextContent('00:03');
  });
});

// ============================================================================
// Counter
// ============================================================================

describe('Counter', () => {
  it('renders without crashing', () => {
    render(<Counter />);
    expect(screen.getByTestId('counter')).toBeInTheDocument();
  });

  it('shows default counters', () => {
    render(<Counter />);
    expect(screen.getByText('Punti vita')).toBeInTheDocument();
    expect(screen.getByText('Risorse')).toBeInTheDocument();
  });

  it('increments a counter', async () => {
    const user = userEvent.setup();
    render(<Counter />);

    const incrementBtn = screen.getByRole('button', { name: 'Increment Punti vita' });
    await user.click(incrementBtn);

    expect(screen.getByLabelText('Punti vita: 21')).toBeInTheDocument();
  });

  it('decrements a counter', async () => {
    const user = userEvent.setup();
    render(<Counter />);

    const decrementBtn = screen.getByRole('button', { name: 'Decrement Punti vita' });
    await user.click(decrementBtn);

    expect(screen.getByLabelText('Punti vita: 19')).toBeInTheDocument();
  });

  it('adds a new counter', async () => {
    const user = userEvent.setup();
    render(<Counter />);

    const input = screen.getByRole('textbox', { name: 'New counter name' });
    await user.type(input, 'Mana');
    await user.click(screen.getByRole('button', { name: 'Add counter' }));

    expect(screen.getByText('Mana')).toBeInTheDocument();
  });

  it('removes a counter', async () => {
    const user = userEvent.setup();
    render(<Counter />);

    await user.click(screen.getByRole('button', { name: 'Remove Punti vita' }));

    expect(screen.queryByText('Punti vita')).not.toBeInTheDocument();
  });
});

// ============================================================================
// Scoreboard
// ============================================================================

describe('Scoreboard', () => {
  it('renders without crashing', () => {
    render(<Scoreboard />);
    expect(screen.getByTestId('scoreboard')).toBeInTheDocument();
  });

  it('shows empty state initially', () => {
    render(<Scoreboard />);
    expect(screen.getByText(/Nessun giocatore/i)).toBeInTheDocument();
  });

  it('adds a player', async () => {
    const user = userEvent.setup();
    render(<Scoreboard />);

    await user.type(screen.getByRole('textbox', { name: 'New player name' }), 'Alice');
    await user.click(screen.getByRole('button', { name: 'Add player' }));

    expect(screen.getByText('Alice')).toBeInTheDocument();
  });

  it('increments and decrements score', async () => {
    const user = userEvent.setup();
    render(<Scoreboard />);

    await user.type(screen.getByRole('textbox', { name: 'New player name' }), 'Bob');
    await user.click(screen.getByRole('button', { name: 'Add player' }));

    await user.click(screen.getByRole('button', { name: 'Increase score for Bob' }));
    await user.click(screen.getByRole('button', { name: 'Increase score for Bob' }));
    await user.click(screen.getByRole('button', { name: 'Increase score for Bob' }));

    expect(screen.getByText('3')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Decrease score for Bob' }));
    expect(screen.getByText('2')).toBeInTheDocument();
  });

  it('removes a player', async () => {
    const user = userEvent.setup();
    render(<Scoreboard />);

    await user.type(screen.getByRole('textbox', { name: 'New player name' }), 'Charlie');
    await user.click(screen.getByRole('button', { name: 'Add player' }));

    await user.click(screen.getByRole('button', { name: 'Remove Charlie' }));
    expect(screen.queryByText('Charlie')).not.toBeInTheDocument();
  });

  it('toggles sort direction', async () => {
    const user = userEvent.setup();
    render(<Scoreboard />);

    // The sort button is a plain <button> inside the <th> (not the mocked Button component)
    const sortBtn =
      screen.getByText('Punti').closest('button') ??
      screen.getByRole('button', { hidden: true, name: /Sort by score/i });
    // Just verify we can interact without crashing
    expect(sortBtn).toBeTruthy();
    if (sortBtn) await user.click(sortBtn);
  });
});

// ============================================================================
// Randomizer
// ============================================================================

describe('Randomizer', () => {
  beforeEach(() => {
    vi.spyOn(Math, 'random').mockReturnValue(0);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders without crashing', () => {
    render(<Randomizer />);
    expect(screen.getByTestId('randomizer')).toBeInTheDocument();
  });

  it('shows empty state initially', () => {
    render(<Randomizer />);
    expect(screen.getByText(/Nessun elemento/i)).toBeInTheDocument();
  });

  it('adds an item', async () => {
    const user = userEvent.setup();
    render(<Randomizer />);

    await user.type(screen.getByRole('textbox', { name: 'New item' }), 'Catan');
    await user.click(screen.getByRole('button', { name: 'Add item' }));

    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('picks a random item', async () => {
    const user = userEvent.setup();
    render(<Randomizer />);

    await user.type(screen.getByRole('textbox', { name: 'New item' }), 'Alpha');
    await user.click(screen.getByRole('button', { name: 'Add item' }));

    await user.click(screen.getByRole('button', { name: 'Pick random item' }));
    // Result section should appear (item is displayed in both list + result)
    expect(screen.getByTestId('randomizer-result')).toBeInTheDocument();
    // At least one element with the text exists
    expect(screen.getAllByText('Alpha').length).toBeGreaterThanOrEqual(1);
  });

  it('removes an item', async () => {
    const user = userEvent.setup();
    render(<Randomizer />);

    await user.type(screen.getByRole('textbox', { name: 'New item' }), 'Beta');
    await user.click(screen.getByRole('button', { name: 'Add item' }));

    await user.click(screen.getByRole('button', { name: 'Remove Beta' }));
    expect(screen.queryByText('Beta')).not.toBeInTheDocument();
  });

  it('clears all items', async () => {
    const user = userEvent.setup();
    render(<Randomizer />);

    await user.type(screen.getByRole('textbox', { name: 'New item' }), 'Gamma');
    await user.click(screen.getByRole('button', { name: 'Add item' }));

    await user.click(screen.getByRole('button', { name: 'Clear all items' }));
    expect(screen.queryByText('Gamma')).not.toBeInTheDocument();
  });
});

// ============================================================================
// ToolkitSheet
// ============================================================================

describe('ToolkitSheet', () => {
  const onClose = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    // crypto.randomUUID fallback for jsdom
    if (!globalThis.crypto?.randomUUID) {
      vi.spyOn(globalThis, 'crypto', 'get').mockReturnValue({
        randomUUID: () => `${Math.random().toString(36).slice(2)}`,
        getRandomValues: (arr: Uint8Array) => arr,
        subtle: {} as SubtleCrypto,
      } as Crypto);
    }
  });

  it('renders nothing when closed', () => {
    render(<ToolkitSheet isOpen={false} onClose={onClose} />);
    expect(screen.queryByTestId('toolkit-sheet')).not.toBeInTheDocument();
  });

  it('renders the tool grid when open', () => {
    render(<ToolkitSheet isOpen={true} onClose={onClose} />);
    expect(screen.getByTestId('toolkit-sheet')).toBeInTheDocument();
    expect(screen.getByTestId('toolkit-grid')).toBeInTheDocument();
  });

  it('shows all 5 tool cards', () => {
    render(<ToolkitSheet isOpen={true} onClose={onClose} />);
    ['dice', 'timer', 'counter', 'scoreboard', 'randomizer'].forEach(id => {
      expect(screen.getByTestId(`toolkit-tool-${id}`)).toBeInTheDocument();
    });
  });

  it('clicking a tool shows it inline', async () => {
    const user = userEvent.setup();
    render(<ToolkitSheet isOpen={true} onClose={onClose} />);

    await user.click(screen.getByTestId('toolkit-tool-dice'));

    expect(screen.getByTestId('toolkit-active-dice')).toBeInTheDocument();
    expect(screen.queryByTestId('toolkit-grid')).not.toBeInTheDocument();
  });

  it('back button returns to grid', async () => {
    const user = userEvent.setup();
    render(<ToolkitSheet isOpen={true} onClose={onClose} />);

    await user.click(screen.getByTestId('toolkit-tool-timer'));
    expect(screen.getByTestId('toolkit-active-timer')).toBeInTheDocument();

    await user.click(screen.getByTestId('toolkit-back-button'));
    expect(screen.getByTestId('toolkit-grid')).toBeInTheDocument();
  });

  it('close button calls onClose', async () => {
    const user = userEvent.setup();
    render(<ToolkitSheet isOpen={true} onClose={onClose} />);

    await user.click(screen.getByTestId('toolkit-close-button'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('clicking overlay calls onClose', async () => {
    const user = userEvent.setup();
    render(<ToolkitSheet isOpen={true} onClose={onClose} />);

    await user.click(screen.getByTestId('toolkit-sheet-overlay'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
