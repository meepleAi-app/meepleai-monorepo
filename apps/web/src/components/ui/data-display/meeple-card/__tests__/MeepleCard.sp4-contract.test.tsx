import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { MeepleCard } from '../MeepleCard';
import { entityIcon } from '../tokens';

describe('MeepleCard SP4 contract (#1856)', () => {
  it('mounts without errors for all 9 entity types using default grid variant', () => {
    const entities = [
      'game',
      'player',
      'session',
      'agent',
      'kb',
      'chat',
      'event',
      'toolkit',
      'tool',
    ] as const;
    for (const e of entities) {
      const { unmount } = render(<MeepleCard entity={e} title={`${e} title`} />);
      expect(screen.getByText(`${e} title`)).toBeInTheDocument();
      unmount();
    }
  });

  it('coverEmoji prop renders in the emoji-band fallback', () => {
    const { container } = render(<MeepleCard entity="session" title="X" coverEmoji="🎲" />);
    // coverEmoji should appear in the cover emoji-band even though EntityBadge has session's 🎯
    const emojiband = container.querySelector('[data-slot="cover-emoji-band"]');
    expect(emojiband?.textContent).toContain('🎲');
  });

  it('omitting coverEmoji falls back to entityIcon for the entity type', () => {
    const { container } = render(<MeepleCard entity="agent" title="Bot" />);
    // Cover should show agent's entityIcon (🤖) in emoji-band fallback
    const emojiband = container.querySelector('[data-slot="cover-emoji-band"]');
    expect(emojiband?.textContent).toContain(entityIcon.agent);
  });

  it('status renders in footer (not top-left stack)', () => {
    const { container } = render(<MeepleCard entity="kb" title="Doc" status="indexed" />);
    const footer = container.querySelector('[data-slot="footer-badge"]');
    expect(footer?.textContent?.toLowerCase()).toContain('indexed');
    const stack = container.querySelector('[data-slot="badge-stack"]');
    expect(stack?.children).toHaveLength(1); // EntityBadge only
  });

  it('hides footer entirely when status and badge are both undefined', () => {
    const { container } = render(<MeepleCard entity="game" title="Catan" />);
    expect(container.querySelector('[data-slot="footer-badge"]')).toBeNull();
  });
});
