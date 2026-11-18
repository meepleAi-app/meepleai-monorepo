import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ChatExtraMeepleCard } from '../ChatExtraMeepleCard';

import type { ChatDetailData } from '../../types';

const reportBggAttempt = vi.hoisted(() => vi.fn());
vi.mock('@/lib/images/bgg-attempt-beacon', () => ({ reportBggAttempt }));

const BGG = 'https://cf.geekdo-images.com/abc/pic1.jpg';
const ALLOWED = 'https://covers.meepleai.app/abc/cover.webp';

const chat: ChatDetailData = {
  id: 'chat-1',
  status: 'active',
  agentName: 'Azul Assistant',
  gameId: 'game-1',
  gameName: 'Azul',
  startedAt: '2024-01-01T00:00:00Z',
  messageCount: 0,
  messages: [],
};

describe('ChatExtraMeepleCard', () => {
  beforeEach(() => {
    reportBggAttempt.mockClear();
  });

  it('shows the gamepad instead of a BGG game thumbnail in the context tab, and an allowed one (#5072)', async () => {
    const user = userEvent.setup();
    const contextGameCard = async (gameThumbnailUrl: string) => {
      const { container, unmount } = render(
        <ChatExtraMeepleCard data={{ ...chat, gameThumbnailUrl }} />
      );
      await user.click(screen.getByRole('tab', { name: /contesto/i }));
      const card = screen.getByText('Gioco').parentElement!.parentElement!;
      const srcs = Array.from(container.querySelectorAll('img'), img => img.getAttribute('src'));
      const fallbackIcon = card.querySelector('svg');
      unmount();
      return { srcs, fallbackIcon };
    };

    const bgg = await contextGameCard(BGG);
    expect(bgg.srcs.some(src => src?.includes('geekdo-images'))).toBe(false);
    expect(bgg.fallbackIcon).not.toBeNull();
    expect(reportBggAttempt).toHaveBeenCalledWith(BGG);

    const allowed = await contextGameCard(ALLOWED);
    expect(allowed.srcs).toEqual([ALLOWED]);
    expect(allowed.fallbackIcon).toBeNull();
  });
});
