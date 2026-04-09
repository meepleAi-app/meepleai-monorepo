import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { ChatSidebar } from '../ChatSidebar';

describe('ChatSidebar', () => {
  const chats = [
    { id: 'c1', emoji: '🎨', title: 'Azul · Turno finale', timestamp: 'Oggi, 14:32', active: true },
    {
      id: 'c2',
      emoji: '🦅',
      title: 'Wingspan · Bonus fine',
      timestamp: 'Ieri, 21:15',
      active: false,
    },
  ];
  const kbGames = [
    { id: 'azul', name: 'Azul', status: 'ready' as const },
    { id: 'wings', name: 'Wingspan', status: 'ready' as const },
    { id: 'everd', name: 'Everdell', status: 'indexing' as const },
  ];

  it('renders the new chat button', () => {
    render(
      <ChatSidebar
        chats={chats}
        kbGames={kbGames}
        onNewChat={() => {}}
        onSelectChat={() => {}}
        onSelectGame={() => {}}
      />
    );
    expect(screen.getByRole('button', { name: /nuova chat/i })).toBeInTheDocument();
  });

  it('renders all recent chats', () => {
    render(
      <ChatSidebar
        chats={chats}
        kbGames={kbGames}
        onNewChat={() => {}}
        onSelectChat={() => {}}
        onSelectGame={() => {}}
      />
    );
    expect(screen.getByText(/Azul · Turno finale/i)).toBeInTheDocument();
    expect(screen.getByText(/Wingspan · Bonus fine/i)).toBeInTheDocument();
  });

  it('renders all KB games with status', () => {
    render(
      <ChatSidebar
        chats={chats}
        kbGames={kbGames}
        onNewChat={() => {}}
        onSelectChat={() => {}}
        onSelectGame={() => {}}
      />
    );
    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Everdell')).toBeInTheDocument();
  });

  it('calls onNewChat when new chat button is clicked', async () => {
    const onNewChat = vi.fn();
    const user = userEvent.setup();
    render(
      <ChatSidebar
        chats={chats}
        kbGames={kbGames}
        onNewChat={onNewChat}
        onSelectChat={() => {}}
        onSelectGame={() => {}}
      />
    );
    await user.click(screen.getByRole('button', { name: /nuova chat/i }));
    expect(onNewChat).toHaveBeenCalled();
  });

  it('calls onSelectChat with chat id', async () => {
    const onSelectChat = vi.fn();
    const user = userEvent.setup();
    render(
      <ChatSidebar
        chats={chats}
        kbGames={kbGames}
        onNewChat={() => {}}
        onSelectChat={onSelectChat}
        onSelectGame={() => {}}
      />
    );
    await user.click(screen.getByRole('button', { name: /Wingspan · Bonus/i }));
    expect(onSelectChat).toHaveBeenCalledWith('c2');
  });

  it('calls onSelectGame with game id', async () => {
    const onSelectGame = vi.fn();
    const user = userEvent.setup();
    render(
      <ChatSidebar
        chats={chats}
        kbGames={kbGames}
        onNewChat={() => {}}
        onSelectChat={() => {}}
        onSelectGame={onSelectGame}
      />
    );
    await user.click(screen.getByRole('button', { name: 'Everdell' }));
    expect(onSelectGame).toHaveBeenCalledWith('everd');
  });
});
