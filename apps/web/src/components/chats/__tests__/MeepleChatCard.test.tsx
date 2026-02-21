/**
 * MeepleChatCard Tests
 * Issue #5002 — Chat Card: azioni contestuali e slot management
 *
 * Tests the action visibility matrix:
 * - Apri/Continua: always visible, disabled if isArchived
 * - Nuova Chat: visible only for owner, disabled if !hasGameChatSlot
 * - Archivia: visible only for owner + !isArchived
 * - Riattiva: visible only for owner + isArchived
 * - Elimina: visible only for owner/admin
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { MeepleChatCard, MeepleChatCardSkeleton } from '../MeepleChatCard';

import type { ChatSessionSummaryDto } from '@/lib/api/schemas/chat-sessions.schemas';

// ============================================================================
// Mock Data
// ============================================================================

const mockActiveSession: ChatSessionSummaryDto = {
  id: 'chat-123',
  userId: 'user-456',
  gameId: 'game-789',
  gameTitle: 'Twilight Imperium',
  title: 'Come funzionano le war suns?',
  messageCount: 12,
  createdAt: '2024-01-01T00:00:00Z',
  lastMessageAt: '2024-06-01T12:00:00Z',
  isArchived: false,
};

const mockArchivedSession: ChatSessionSummaryDto = {
  ...mockActiveSession,
  id: 'chat-456',
  title: 'Regole sui pianeti home',
  isArchived: true,
  lastMessageAt: '2024-03-01T10:00:00Z',
};

const mockUntitledSession: ChatSessionSummaryDto = {
  ...mockActiveSession,
  id: 'chat-789',
  title: null,
  messageCount: 0,
  lastMessageAt: null,
};

// ============================================================================
// Tests
// ============================================================================

describe('MeepleChatCard', () => {
  // --------------------------------------------------------------------------
  // Basic rendering
  // --------------------------------------------------------------------------

  it('renders session title as card title', () => {
    render(<MeepleChatCard session={mockActiveSession} />);
    expect(screen.getByText('Come funzionano le war suns?')).toBeInTheDocument();
  });

  it('renders fallback title for untitled session', () => {
    render(<MeepleChatCard session={mockUntitledSession} />);
    expect(screen.getByText('Chat senza titolo')).toBeInTheDocument();
  });

  it('renders with correct data-testid', () => {
    render(<MeepleChatCard session={mockActiveSession} />);
    expect(screen.getByTestId('chat-card-chat-123')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // AC: Apri/Continua — always visible, disabled if isArchived
  // --------------------------------------------------------------------------

  it('Apri/Continua is visible and enabled for active chat', () => {
    render(<MeepleChatCard session={mockActiveSession} />);
    const button = screen.getByRole('button', { name: 'Apri/Continua' });
    expect(button).toBeInTheDocument();
    expect(button).not.toBeDisabled();
  });

  it('Apri/Continua is disabled for archived chat with tooltip label', () => {
    render(<MeepleChatCard session={mockArchivedSession} />);
    const button = screen.getByRole('button', { name: 'Chat archiviata' });
    expect(button).toBeInTheDocument();
    expect(button).toBeDisabled();
  });

  // --------------------------------------------------------------------------
  // AC: Nuova Chat — visible only for owner, disabled if !hasGameChatSlot
  // --------------------------------------------------------------------------

  it('Nuova Chat not shown for non-owner user', () => {
    render(<MeepleChatCard session={mockActiveSession} isOwner={false} />);
    expect(screen.queryByRole('button', { name: 'Nuova Chat' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Limite chat raggiunto' })).not.toBeInTheDocument();
  });

  it('Nuova Chat shown and enabled for owner with available slot', () => {
    render(<MeepleChatCard session={mockActiveSession} isOwner={true} hasGameChatSlot={true} />);
    const button = screen.getByRole('button', { name: 'Nuova Chat' });
    expect(button).toBeInTheDocument();
    expect(button).not.toBeDisabled();
  });

  it('Nuova Chat shown but disabled for owner when no slot available', () => {
    render(<MeepleChatCard session={mockActiveSession} isOwner={true} hasGameChatSlot={false} />);
    const button = screen.getByRole('button', { name: 'Limite chat raggiunto' });
    expect(button).toBeInTheDocument();
    expect(button).toBeDisabled();
  });

  it('Nuova Chat calls onNewChat with gameId when clicked', async () => {
    const user = userEvent.setup();
    const onNewChat = vi.fn();

    render(
      <MeepleChatCard
        session={mockActiveSession}
        isOwner={true}
        hasGameChatSlot={true}
        onNewChat={onNewChat}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Nuova Chat' }));
    expect(onNewChat).toHaveBeenCalledWith('game-789');
  });

  // --------------------------------------------------------------------------
  // AC: Archivia — visible only for owner + !isArchived
  // --------------------------------------------------------------------------

  it('Archivia not shown for non-owner user', () => {
    render(<MeepleChatCard session={mockActiveSession} isOwner={false} />);
    expect(screen.queryByRole('button', { name: 'Archivia' })).not.toBeInTheDocument();
  });

  it('Archivia not shown for owner on archived chat', () => {
    render(<MeepleChatCard session={mockArchivedSession} isOwner={true} />);
    expect(screen.queryByRole('button', { name: 'Archivia' })).not.toBeInTheDocument();
  });

  it('Archivia shown for owner on active chat', () => {
    render(<MeepleChatCard session={mockActiveSession} isOwner={true} />);
    expect(screen.getByRole('button', { name: 'Archivia' })).toBeInTheDocument();
  });

  it('Archivia calls onArchive with session id when clicked', async () => {
    const user = userEvent.setup();
    const onArchive = vi.fn();

    render(
      <MeepleChatCard
        session={mockActiveSession}
        isOwner={true}
        onArchive={onArchive}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Archivia' }));
    expect(onArchive).toHaveBeenCalledWith('chat-123');
  });

  // --------------------------------------------------------------------------
  // AC: Riattiva — visible only for owner + isArchived
  // --------------------------------------------------------------------------

  it('Riattiva not shown for non-owner user', () => {
    render(<MeepleChatCard session={mockArchivedSession} isOwner={false} />);
    expect(screen.queryByRole('button', { name: 'Riattiva' })).not.toBeInTheDocument();
  });

  it('Riattiva not shown for owner on active chat', () => {
    render(<MeepleChatCard session={mockActiveSession} isOwner={true} />);
    expect(screen.queryByRole('button', { name: 'Riattiva' })).not.toBeInTheDocument();
  });

  it('Riattiva shown for owner on archived chat', () => {
    render(<MeepleChatCard session={mockArchivedSession} isOwner={true} />);
    expect(screen.getByRole('button', { name: 'Riattiva' })).toBeInTheDocument();
  });

  it('Riattiva calls onReactivate with session id when clicked', async () => {
    const user = userEvent.setup();
    const onReactivate = vi.fn();

    render(
      <MeepleChatCard
        session={mockArchivedSession}
        isOwner={true}
        onReactivate={onReactivate}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Riattiva' }));
    expect(onReactivate).toHaveBeenCalledWith('chat-456');
  });

  // --------------------------------------------------------------------------
  // AC: Elimina — visible only for owner/admin
  // --------------------------------------------------------------------------

  it('Elimina not shown for non-owner non-admin user', () => {
    render(<MeepleChatCard session={mockActiveSession} isOwner={false} isAdmin={false} />);
    expect(screen.queryByRole('button', { name: 'Elimina' })).not.toBeInTheDocument();
  });

  it('Elimina shown for owner', () => {
    render(<MeepleChatCard session={mockActiveSession} isOwner={true} isAdmin={false} />);
    expect(screen.getByRole('button', { name: 'Elimina' })).toBeInTheDocument();
  });

  it('Elimina shown for admin', () => {
    render(<MeepleChatCard session={mockActiveSession} isOwner={false} isAdmin={true} />);
    expect(screen.getByRole('button', { name: 'Elimina' })).toBeInTheDocument();
  });

  it('Elimina calls onDelete with session id and title when clicked', async () => {
    const user = userEvent.setup();
    const onDelete = vi.fn();

    render(
      <MeepleChatCard
        session={mockActiveSession}
        isOwner={true}
        onDelete={onDelete}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Elimina' }));
    expect(onDelete).toHaveBeenCalledWith('chat-123', 'Come funzionano le war suns?');
  });

  it('Elimina calls onDelete with fallback title for untitled session', async () => {
    const user = userEvent.setup();
    const onDelete = vi.fn();

    render(
      <MeepleChatCard
        session={mockUntitledSession}
        isOwner={true}
        onDelete={onDelete}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Elimina' }));
    expect(onDelete).toHaveBeenCalledWith('chat-789', 'Chat');
  });

  // --------------------------------------------------------------------------
  // AC: No Configura/Edit action
  // --------------------------------------------------------------------------

  it('does not render Configura action', () => {
    render(<MeepleChatCard session={mockActiveSession} isOwner={true} isAdmin={true} />);
    expect(screen.queryByRole('button', { name: 'Configura' })).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Unauthenticated / no-role user
  // --------------------------------------------------------------------------

  it('unauthenticated user sees only Apri/Continua and no owner/admin actions', () => {
    render(<MeepleChatCard session={mockActiveSession} />);
    // Apri/Continua always visible
    expect(screen.getByRole('button', { name: 'Apri/Continua' })).toBeInTheDocument();
    // Owner/admin actions hidden
    expect(screen.queryByRole('button', { name: 'Nuova Chat' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Archivia' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Elimina' })).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Skeleton
  // --------------------------------------------------------------------------

  it('renders skeleton with correct testid', () => {
    render(<MeepleChatCardSkeleton />);
    // MeepleCard in loading mode hardcodes data-testid="meeple-card-skeleton"
    expect(screen.getByTestId('meeple-card-skeleton')).toBeInTheDocument();
  });
});
