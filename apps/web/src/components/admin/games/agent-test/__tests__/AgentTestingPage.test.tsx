/**
 * AgentTestingPage Component Tests
 * Issue #4673: Agent testing tab navigation and rendering coverage.
 *
 * Tests:
 * - Renders page title with game title
 * - Shows "Auto Test" tab as active by default
 * - Renders AutoTestSuite in auto-test tab
 * - Clicking "Interactive Chat" tab shows InteractiveChat component
 * - Clicking back shows AutoTestSuite again
 * - Back link present pointing to /admin/shared-games/all
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';

import { AgentTestingPage } from '../AgentTestingPage';

// ─── Mocks ───────────────────────────────────────────────────────────────────

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

vi.mock('../AutoTestSuite', () => ({
  AutoTestSuite: ({ gameId }: { gameId: string }) => (
    <div data-testid="auto-test-suite" data-game-id={gameId}>
      AutoTestSuite
    </div>
  ),
}));

vi.mock('../InteractiveChat', () => ({
  InteractiveChat: ({ gameId, gameTitle }: { gameId: string; gameTitle?: string }) => (
    <div data-testid="interactive-chat" data-game-id={gameId} data-game-title={gameTitle}>
      InteractiveChat
    </div>
  ),
}));

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('AgentTestingPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render page title with game title', () => {
    render(<AgentTestingPage gameId="game-uuid" gameTitle="Gloomhaven" />);

    expect(screen.getByText(/Test Agent: Gloomhaven/i)).toBeDefined();
  });

  it('should render fallback title "Game" when gameTitle is not provided', () => {
    render(<AgentTestingPage gameId="game-uuid" />);

    expect(screen.getByText(/Test Agent: Game/i)).toBeDefined();
  });

  it('should show Auto Test tab as active by default', () => {
    render(<AgentTestingPage gameId="game-uuid" gameTitle="Catan" />);

    expect(screen.getByRole('button', { name: /Auto Test/i })).toBeDefined();
    expect(screen.getByTestId('auto-test-suite')).toBeDefined();
  });

  it('should render AutoTestSuite with correct gameId in auto-test tab', () => {
    render(<AgentTestingPage gameId="game-uuid-1" gameTitle="Catan" />);

    const suite = screen.getByTestId('auto-test-suite');
    expect(suite.getAttribute('data-game-id')).toBe('game-uuid-1');
  });

  it('should NOT render InteractiveChat when auto-test tab is active', () => {
    render(<AgentTestingPage gameId="game-uuid" />);

    expect(screen.queryByTestId('interactive-chat')).toBeNull();
  });

  it('should show InteractiveChat when "Interactive Chat" tab is clicked', async () => {
    const user = userEvent.setup();
    render(<AgentTestingPage gameId="game-uuid" gameTitle="Pandemic" />);

    const chatTab = screen.getByRole('button', { name: /Interactive Chat/i });
    await user.click(chatTab);

    expect(screen.getByTestId('interactive-chat')).toBeDefined();
    expect(screen.queryByTestId('auto-test-suite')).toBeNull();
  });

  it('should pass gameId and gameTitle to InteractiveChat', async () => {
    const user = userEvent.setup();
    render(<AgentTestingPage gameId="game-uuid-2" gameTitle="Spirit Island" />);

    await user.click(screen.getByRole('button', { name: /Interactive Chat/i }));

    const chat = screen.getByTestId('interactive-chat');
    expect(chat.getAttribute('data-game-id')).toBe('game-uuid-2');
    expect(chat.getAttribute('data-game-title')).toBe('Spirit Island');
  });

  it('should switch back to AutoTestSuite when "Auto Test" tab is clicked again', async () => {
    const user = userEvent.setup();
    render(<AgentTestingPage gameId="game-uuid" />);

    // Switch to chat
    await user.click(screen.getByRole('button', { name: /Interactive Chat/i }));
    expect(screen.getByTestId('interactive-chat')).toBeDefined();

    // Switch back
    await user.click(screen.getByRole('button', { name: /Auto Test/i }));
    expect(screen.getByTestId('auto-test-suite')).toBeDefined();
    expect(screen.queryByTestId('interactive-chat')).toBeNull();
  });

  it('should render back link pointing to /admin/shared-games/all', () => {
    render(<AgentTestingPage gameId="game-uuid" />);

    const backLink = screen.getByRole('link', { name: '' });
    expect(backLink.getAttribute('href')).toBe('/admin/shared-games/all');
  });

  it('should render both tab buttons', () => {
    render(<AgentTestingPage gameId="game-uuid" />);

    expect(screen.getByRole('button', { name: /Auto Test/i })).toBeDefined();
    expect(screen.getByRole('button', { name: /Interactive Chat/i })).toBeDefined();
  });
});
