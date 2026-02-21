/**
 * ChatHistoryTable Tests
 * Issue #4917: Admin chat history - replaced static mock with real ChatThread data.
 *
 * Tests:
 * 1. Shows loading state while API call is in-flight
 * 2. Renders table headers after data loads
 * 3. Renders session rows from API response
 * 4. Expands chat preview on row click
 * 5. Collapses preview on second click
 * 6. Handles keyboard navigation (Enter key)
 * 7. Handles keyboard navigation (Space key)
 * 8. Shows error state on API failure
 * 9. Shows empty state when no sessions returned
 * 10. Formats duration correctly
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import React from 'react';

// ─── Mock adminDashboardClient ─────────────────────────────────────────────────

const mockGetChatHistory = vi.fn();

vi.mock('@/lib/api/clients/adminDashboardClient', () => ({
  adminDashboardClient: {
    getChatHistory: () => mockGetChatHistory(),
  },
}));

import { ChatHistoryTable } from '../chat-history-table';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const SESSION_1 = {
  id: 'session-uuid-0001',
  userId: 'user-uuid-0001',
  userName: 'Alice Rossi',
  agent: 'Tutor',
  messageCount: 8,
  durationSeconds: 245,
  date: '2026-02-20T14:30:00.000Z',
  preview: [
    { role: 'user', content: 'Come funziona la fase di produzione in Catan?' },
    { role: 'assistant', content: 'Nella fase di produzione si tirano i dadi...' },
  ],
};

const SESSION_2 = {
  id: 'session-uuid-0002',
  userId: 'user-uuid-0002',
  userName: 'Marco Bianchi',
  agent: 'Arbitro',
  messageCount: 3,
  durationSeconds: 72,
  date: '2026-02-20T10:00:00.000Z',
  preview: [],
};

const MOCK_RESPONSE = {
  sessions: [SESSION_1, SESSION_2],
  total: 2,
  page: 1,
  pageSize: 20,
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('ChatHistoryTable', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows loading indicator while fetching', () => {
    mockGetChatHistory.mockReturnValue(new Promise(() => {}));
    render(<ChatHistoryTable />);
    expect(screen.getByText(/caricamento sessioni/i)).toBeInTheDocument();
  });

  it('renders table headers after data loads', async () => {
    mockGetChatHistory.mockResolvedValue(MOCK_RESPONSE);
    render(<ChatHistoryTable />);
    await waitFor(() => expect(screen.getByText('Session ID')).toBeInTheDocument());
    expect(screen.getByText('User')).toBeInTheDocument();
    expect(screen.getByText('Agent')).toBeInTheDocument();
    expect(screen.getByText('Messages')).toBeInTheDocument();
    expect(screen.getByText('Duration')).toBeInTheDocument();
    expect(screen.getByText('Date')).toBeInTheDocument();
  });

  it('renders session rows with user names', async () => {
    mockGetChatHistory.mockResolvedValue(MOCK_RESPONSE);
    render(<ChatHistoryTable />);
    await waitFor(() => expect(screen.getByText('Alice Rossi')).toBeInTheDocument());
    expect(screen.getByText('Marco Bianchi')).toBeInTheDocument();
  });

  it('renders truncated session IDs', async () => {
    mockGetChatHistory.mockResolvedValue(MOCK_RESPONSE);
    render(<ChatHistoryTable />);
    // Component renders id.slice(0,8) + '...' — compute expected from fixture
    const truncated = SESSION_1.id.slice(0, 8) + '...';
    await waitFor(() =>
      expect(screen.getAllByText(truncated).length).toBeGreaterThan(0)
    );
  });

  it('renders agent badges', async () => {
    mockGetChatHistory.mockResolvedValue(MOCK_RESPONSE);
    render(<ChatHistoryTable />);
    await waitFor(() => expect(screen.getByText('Tutor')).toBeInTheDocument());
    expect(screen.getByText('Arbitro')).toBeInTheDocument();
  });

  it('expands chat preview on row click', async () => {
    mockGetChatHistory.mockResolvedValue(MOCK_RESPONSE);
    render(<ChatHistoryTable />);
    await waitFor(() => screen.getByText('Alice Rossi'));
    const row = screen.getByText('Alice Rossi').closest('tr')!;
    // Initially collapsed
    expect(screen.queryByText('Chat Preview')).not.toBeInTheDocument();
    fireEvent.click(row);
    expect(screen.getByText('Chat Preview')).toBeInTheDocument();
  });

  it('collapses preview on second click', async () => {
    mockGetChatHistory.mockResolvedValue(MOCK_RESPONSE);
    render(<ChatHistoryTable />);
    await waitFor(() => screen.getByText('Alice Rossi'));
    const row = screen.getByText('Alice Rossi').closest('tr')!;
    fireEvent.click(row);
    expect(screen.getByText('Chat Preview')).toBeInTheDocument();
    fireEvent.click(row);
    expect(screen.queryByText('Chat Preview')).not.toBeInTheDocument();
  });

  it('shows preview message content when expanded', async () => {
    mockGetChatHistory.mockResolvedValue(MOCK_RESPONSE);
    render(<ChatHistoryTable />);
    await waitFor(() => screen.getByText('Alice Rossi'));
    fireEvent.click(screen.getByText('Alice Rossi').closest('tr')!);
    expect(screen.getByText(/come funziona la fase di produzione/i)).toBeInTheDocument();
  });

  it('handles keyboard Enter key to expand row', async () => {
    mockGetChatHistory.mockResolvedValue(MOCK_RESPONSE);
    render(<ChatHistoryTable />);
    await waitFor(() => screen.getByText('Alice Rossi'));
    const row = screen.getByText('Alice Rossi').closest('tr')!;
    fireEvent.keyDown(row, { key: 'Enter' });
    expect(screen.getByText('Chat Preview')).toBeInTheDocument();
  });

  it('handles keyboard Space key to expand row', async () => {
    mockGetChatHistory.mockResolvedValue(MOCK_RESPONSE);
    render(<ChatHistoryTable />);
    await waitFor(() => screen.getByText('Alice Rossi'));
    const row = screen.getByText('Alice Rossi').closest('tr')!;
    fireEvent.keyDown(row, { key: ' ' });
    expect(screen.getByText('Chat Preview')).toBeInTheDocument();
  });

  it('shows error state when API call fails', async () => {
    mockGetChatHistory.mockRejectedValue(new Error('Network error'));
    render(<ChatHistoryTable />);
    await waitFor(() =>
      expect(screen.getByText(/impossibile caricare la cronologia chat/i)).toBeInTheDocument()
    );
  });

  it('shows empty state when no sessions returned', async () => {
    mockGetChatHistory.mockResolvedValue({ sessions: [], total: 0, page: 1, pageSize: 20 });
    render(<ChatHistoryTable />);
    await waitFor(() =>
      expect(screen.getByText(/nessuna sessione chat trovata/i)).toBeInTheDocument()
    );
  });

  it('formats duration in minutes and seconds', async () => {
    mockGetChatHistory.mockResolvedValue(MOCK_RESPONSE);
    render(<ChatHistoryTable />);
    // SESSION_1 has 245s = 4m 5s
    await waitFor(() => expect(screen.getByText('4m 5s')).toBeInTheDocument());
    // SESSION_2 has 72s = 1m 12s
    expect(screen.getByText('1m 12s')).toBeInTheDocument();
  });

  it('shows empty preview message when session has no preview', async () => {
    mockGetChatHistory.mockResolvedValue(MOCK_RESPONSE);
    render(<ChatHistoryTable />);
    await waitFor(() => screen.getByText('Marco Bianchi'));
    fireEvent.click(screen.getByText('Marco Bianchi').closest('tr')!);
    expect(screen.getByText(/nessun messaggio da visualizzare/i)).toBeInTheDocument();
  });
});
