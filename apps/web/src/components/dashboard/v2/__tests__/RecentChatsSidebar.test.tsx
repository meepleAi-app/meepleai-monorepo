/**
 * RecentChatsSidebar — Unit Tests
 * Dashboard v2 "Il Tavolo"
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { RecentChatsSidebar } from '../RecentChatsSidebar';
import type { ChatThread } from '../RecentChatsSidebar';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const makeThread = (id: string, title: string, messageCount = 5): ChatThread => ({
  id,
  title,
  messageCount,
  agentName: 'Agente Test',
  lastMessageAt: new Date(Date.now() - 10 * 60000).toISOString(), // 10 min ago
});

const MOCK_THREADS: ChatThread[] = [
  makeThread('t1', 'Regole di Catan', 3),
  makeThread('t2', 'Strategia Ticket to Ride', 7),
  makeThread('t3', 'Domande su Wingspan', 2),
  makeThread('t4', 'Setup Terraforming Mars', 12),
];

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('RecentChatsSidebar', () => {
  it('renders section title with correct text', () => {
    render(<RecentChatsSidebar threads={MOCK_THREADS} />);

    expect(screen.getByTestId('sidebar-chats')).toBeInTheDocument();
    expect(screen.getByText('💬 Chat Recenti')).toBeInTheDocument();
  });

  it('renders correct number of items (max 4)', () => {
    const fiveThreads = [...MOCK_THREADS, makeThread('t5', 'Thread Extra')];
    render(<RecentChatsSidebar threads={fiveThreads} />);

    expect(screen.getByText('Regole di Catan')).toBeInTheDocument();
    expect(screen.getByText('Strategia Ticket to Ride')).toBeInTheDocument();
    expect(screen.getByText('Domande su Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Setup Terraforming Mars')).toBeInTheDocument();
    expect(screen.queryByText('Thread Extra')).not.toBeInTheDocument();
  });

  it('renders all 4 items when exactly 4 threads provided', () => {
    render(<RecentChatsSidebar threads={MOCK_THREADS} />);

    MOCK_THREADS.forEach(t => {
      expect(screen.getByText(t.title)).toBeInTheDocument();
    });
  });

  it('hides section when threads array is empty', () => {
    const { container } = render(<RecentChatsSidebar threads={[]} />);

    expect(screen.queryByTestId('sidebar-chats')).not.toBeInTheDocument();
    expect(container.firstChild).toBeNull();
  });

  it('renders skeletons when loading', () => {
    render(<RecentChatsSidebar threads={[]} loading />);

    expect(screen.getByTestId('sidebar-chats')).toBeInTheDocument();
    const skeletons = screen
      .getAllByRole('generic')
      .filter(el => el.className.includes('animate-pulse'));
    expect(skeletons.length).toBeGreaterThanOrEqual(3);
    expect(screen.queryByText('Regole di Catan')).not.toBeInTheDocument();
  });

  it('shows section title even when loading', () => {
    render(<RecentChatsSidebar threads={[]} loading />);

    expect(screen.getByText('💬 Chat Recenti')).toBeInTheDocument();
  });

  it('renders message count and agent name in subtitle', () => {
    render(<RecentChatsSidebar threads={[makeThread('x', 'Chat Test', 9)]} />);

    expect(screen.getByText(/9 msg · con Agente Test/)).toBeInTheDocument();
  });

  it('renders relative time for each thread', () => {
    render(<RecentChatsSidebar threads={[makeThread('x', 'Chat Test')]} />);

    expect(screen.getByText(/10 min fa/)).toBeInTheDocument();
  });
});
