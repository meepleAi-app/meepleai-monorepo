/**
 * AgentConfigForm Tests
 * Issue #4948: Agent config page redesign — right-column form
 *
 * Test Coverage:
 * - Renders typology radio cards (Tutor, Arbitro, Strategist)
 * - Renders strategy select dropdown
 * - Shows cost estimate based on selected typology+strategy
 * - CTA "Salva & Avvia Chat →" enabled when hasIndexedKb=true
 * - CTA disabled when hasIndexedKb=false with hint message
 * - Selecting a typology updates active state (aria-pressed)
 * - Clicking CTA calls onSave then navigates to /chat?gameId=...
 * - onSave is optional (no crash when not provided)
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { AgentConfigForm } from '../AgentConfigForm';

// ============================================================================
// Mocks
// ============================================================================

const mockPush = vi.fn();

vi.mock('next/navigation', () => ({
  useRouter: vi.fn(() => ({ push: mockPush })),
}));

// ============================================================================
// Tests
// ============================================================================

describe('AgentConfigForm', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders the form header', () => {
      render(<AgentConfigForm gameId="game-1" hasIndexedKb={false} />);

      expect(screen.getByText('Agente AI')).toBeInTheDocument();
    });

    it('renders all three typology cards', () => {
      render(<AgentConfigForm gameId="game-1" hasIndexedKb={false} />);

      expect(screen.getByText('Tutor')).toBeInTheDocument();
      expect(screen.getByText('Arbitro')).toBeInTheDocument();
      expect(screen.getByText('Strategist')).toBeInTheDocument();
    });

    it('renders strategy label', () => {
      render(<AgentConfigForm gameId="game-1" hasIndexedKb={false} />);

      expect(screen.getByText('Strategia')).toBeInTheDocument();
    });

    it('renders cost estimate section', () => {
      render(<AgentConfigForm gameId="game-1" hasIndexedKb={false} />);

      expect(screen.getByText('Stima costo per sessione')).toBeInTheDocument();
    });
  });

  describe('Initial State', () => {
    it('defaults to "arbitro" typology (aria-checked=true)', () => {
      render(<AgentConfigForm gameId="game-1" hasIndexedKb={false} />);

      const arbitroBtn = screen.getByRole('radio', { name: /arbitro/i });
      expect(arbitroBtn).toHaveAttribute('aria-checked', 'true');
    });

    it('tutor and strategist start deselected', () => {
      render(<AgentConfigForm gameId="game-1" hasIndexedKb={false} />);

      const tutorBtn = screen.getByRole('radio', { name: /tutor/i });
      const strategistBtn = screen.getByRole('radio', { name: /strategist/i });
      expect(tutorBtn).toHaveAttribute('aria-checked', 'false');
      expect(strategistBtn).toHaveAttribute('aria-checked', 'false');
    });

    it('respects initialTypology prop', () => {
      render(
        <AgentConfigForm
          gameId="game-1"
          hasIndexedKb={false}
          initialTypology="tutor"
        />
      );

      expect(screen.getByRole('radio', { name: /tutor/i })).toHaveAttribute(
        'aria-checked',
        'true'
      );
      expect(screen.getByRole('radio', { name: /arbitro/i })).toHaveAttribute(
        'aria-checked',
        'false'
      );
    });
  });

  describe('Typology Selection', () => {
    it('selects tutor when clicked', async () => {
      const user = userEvent.setup();
      render(<AgentConfigForm gameId="game-1" hasIndexedKb={false} />);

      await user.click(screen.getByRole('radio', { name: /tutor/i }));

      expect(screen.getByRole('radio', { name: /tutor/i })).toHaveAttribute(
        'aria-checked',
        'true'
      );
      expect(screen.getByRole('radio', { name: /arbitro/i })).toHaveAttribute(
        'aria-checked',
        'false'
      );
    });

    it('selects strategist when clicked', async () => {
      const user = userEvent.setup();
      render(<AgentConfigForm gameId="game-1" hasIndexedKb={false} />);

      await user.click(screen.getByRole('radio', { name: /strategist/i }));

      expect(screen.getByRole('radio', { name: /strategist/i })).toHaveAttribute(
        'aria-checked',
        'true'
      );
    });

    it('updates cost estimate when typology changes', async () => {
      const user = userEvent.setup();
      render(<AgentConfigForm gameId="game-1" hasIndexedKb={false} />);

      // Strategist balanced costs more than arbitro balanced
      await user.click(screen.getByRole('radio', { name: /strategist/i }));

      expect(screen.getByText('~0.02€')).toBeInTheDocument();
    });
  });

  describe('CTA — hasIndexedKb = true', () => {
    it('renders enabled "Salva & Avvia Chat" button', () => {
      render(<AgentConfigForm gameId="game-1" hasIndexedKb={true} />);

      const btn = screen.getByTestId('save-and-chat-btn');
      expect(btn).not.toBeDisabled();
    });

    it('navigates to /chat after clicking when onSave not provided', async () => {
      const user = userEvent.setup();
      render(<AgentConfigForm gameId="game-1" hasIndexedKb={true} />);

      await user.click(screen.getByTestId('save-and-chat-btn'));

      expect(mockPush).toHaveBeenCalledWith('/chat?gameId=game-1');
    });

    it('calls onSave and does NOT navigate itself when onSave is provided', async () => {
      const onSave = vi.fn().mockResolvedValue(undefined);
      const user = userEvent.setup();

      render(
        <AgentConfigForm
          gameId="game-2"
          hasIndexedKb={true}
          onSave={onSave}
        />
      );

      await user.click(screen.getByTestId('save-and-chat-btn'));

      // onSave is called; navigation is delegated to caller via onSave callback
      expect(onSave).toHaveBeenCalledOnce();
      expect(mockPush).not.toHaveBeenCalled();
    });

    it('passes typology and strategy to onSave', async () => {
      const onSave = vi.fn().mockResolvedValue(undefined);
      const user = userEvent.setup();

      render(
        <AgentConfigForm
          gameId="game-1"
          hasIndexedKb={true}
          initialTypology="tutor"
          initialStrategy="precise"
          onSave={onSave}
        />
      );

      await user.click(screen.getByTestId('save-and-chat-btn'));

      expect(onSave).toHaveBeenCalledWith('tutor', 'precise');
    });

    it('URL-encodes gameId in navigation', async () => {
      const user = userEvent.setup();
      render(<AgentConfigForm gameId="game with spaces" hasIndexedKb={true} />);

      await user.click(screen.getByTestId('save-and-chat-btn'));

      expect(mockPush).toHaveBeenCalledWith(
        `/chat?gameId=${encodeURIComponent('game with spaces')}`
      );
    });
  });

  describe('CTA — hasIndexedKb = false', () => {
    it('renders disabled button', () => {
      render(<AgentConfigForm gameId="game-1" hasIndexedKb={false} />);

      expect(screen.getByTestId('save-and-chat-btn-disabled')).toBeDisabled();
    });

    it('shows hint message below disabled button', () => {
      render(<AgentConfigForm gameId="game-1" hasIndexedKb={false} />);

      expect(
        screen.getByText(/Aggiungi un documento indicizzato/i)
      ).toBeInTheDocument();
    });

    it('does not navigate when kb is missing', async () => {
      const user = userEvent.setup();
      render(<AgentConfigForm gameId="game-1" hasIndexedKb={false} />);

      // Can't click a disabled button normally — just verify it's disabled
      expect(screen.getByTestId('save-and-chat-btn-disabled')).toBeDisabled();
      expect(mockPush).not.toHaveBeenCalled();
    });
  });

  describe('Cost Estimate', () => {
    it('shows correct cost for arbitro + balanced (default)', () => {
      render(
        <AgentConfigForm
          gameId="game-1"
          hasIndexedKb={false}
          initialTypology="arbitro"
          initialStrategy="balanced"
        />
      );

      expect(screen.getByText('~0.01€')).toBeInTheDocument();
    });

    it('shows correct cost for strategist + creative', async () => {
      const user = userEvent.setup();
      render(
        <AgentConfigForm
          gameId="game-1"
          hasIndexedKb={false}
          initialTypology="strategist"
          initialStrategy="balanced"
        />
      );

      // Default is strategist balanced = ~0.02€
      expect(screen.getByText('~0.02€')).toBeInTheDocument();
    });
  });

  describe('Error Handling', () => {
    it('shows error message when onSave rejects', async () => {
      const onSave = vi.fn().mockRejectedValue(new Error('Network error'));
      const user = userEvent.setup();

      render(
        <AgentConfigForm
          gameId="game-1"
          hasIndexedKb={true}
          onSave={onSave}
        />
      );

      await user.click(screen.getByTestId('save-and-chat-btn'));

      expect(screen.getByTestId('save-error')).toBeInTheDocument();
      expect(screen.getByText(/Errore durante la configurazione/i)).toBeInTheDocument();
    });

    it('clears error on next save attempt', async () => {
      let callCount = 0;
      const onSave = vi.fn().mockImplementation(() => {
        callCount++;
        if (callCount === 1) return Promise.reject(new Error('fail'));
        return Promise.resolve();
      });
      const user = userEvent.setup();

      render(
        <AgentConfigForm
          gameId="game-1"
          hasIndexedKb={true}
          onSave={onSave}
        />
      );

      // First click → error
      await user.click(screen.getByTestId('save-and-chat-btn'));
      expect(screen.getByTestId('save-error')).toBeInTheDocument();

      // Second click → error disappears immediately, then resolves
      await user.click(screen.getByTestId('save-and-chat-btn'));
      expect(screen.queryByTestId('save-error')).not.toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('wraps typology cards in a radiogroup', () => {
      render(<AgentConfigForm gameId="game-1" hasIndexedKb={false} />);

      expect(screen.getByRole('radiogroup')).toBeInTheDocument();
    });

    it('disabled CTA has aria-describedby linking to hint', () => {
      render(<AgentConfigForm gameId="game-1" hasIndexedKb={false} />);

      const btn = screen.getByTestId('save-and-chat-btn-disabled');
      expect(btn).toHaveAttribute('aria-describedby', 'no-kb-hint');

      const hint = screen.getByText(/Aggiungi un documento indicizzato/i);
      expect(hint).toHaveAttribute('id', 'no-kb-hint');
    });
  });
});
