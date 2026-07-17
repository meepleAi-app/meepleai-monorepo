import { render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { describe, expect, it, vi } from 'vitest';

import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';

import { FlavorRenderer, hasFlavor } from '../FlavorRenderer';

vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({
  useUpdateLiveGameState: () => ({ mutate: vi.fn() }),
}));

// #2788: flavor components self-build labels via useTranslation/useIntl now,
// so tests render through an IntlProvider. `onError` swallows react-intl
// MISSING_TRANSLATION noise (empty messages → t() returns the raw key).
const PANEL_ARIA_KEY = 'pages.sessionLive.flavor.catan.panelAriaLabel';
const WAITING_KEY = 'pages.sessionLive.flavor.catan.viewerWaiting';

function renderWithIntl(ui: React.ReactElement) {
  return render(
    <IntlProvider locale="en" messages={{}} onError={() => {}}>
      {ui}
    </IntlProvider>
  );
}

const SESSION = {
  gameSlug: 'catan',
  currentTurnIndex: 0,
  currentTurnPlayerId: null,
  scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
  roundScores: [],
  players: [
    {
      id: 'p1',
      userId: null,
      displayName: 'Alice',
      avatarUrl: null,
      color: 'Red',
      role: 'Player',
      teamId: null,
      totalScore: 5,
      currentRank: 1,
      joinedAt: '',
      isActive: false,
    },
  ],
} as unknown as LiveSessionDto;

describe('hasFlavor', () => {
  it('is true for catan and wingspan, false for unknown / null / undefined', () => {
    expect(hasFlavor('catan')).toBe(true);
    expect(hasFlavor('wingspan')).toBe(true);
    expect(hasFlavor('codenames')).toBe(true);
    expect(hasFlavor('puerto-rico')).toBe(true);
    expect(hasFlavor('paleo')).toBe(true);
    expect(hasFlavor('chess')).toBe(false);
    expect(hasFlavor(null)).toBe(false);
    expect(hasFlavor(undefined)).toBe(false);
  });
});

describe('FlavorRenderer', () => {
  it('returns null for a game without a flavor', () => {
    const { container } = render(
      <FlavorRenderer
        gameSlug="chess"
        view="live"
        session={SESSION}
        viewerRole="Player"
        sessionId="s1"
      />
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('lazy-loads and renders the Catan flavor for gameSlug=catan, forwarding viewerRole/sessionId', async () => {
    renderWithIntl(
      <FlavorRenderer
        gameSlug="catan"
        view="live"
        session={SESSION}
        viewerRole="Player"
        sessionId="s1"
      />
    );
    // <section aria-label={t(...)}> → implicit role="region"
    expect(await screen.findByRole('region', { name: PANEL_ARIA_KEY })).toBeInTheDocument();
    // Player (non-Host) sees the waiting message, not the host CTA — proves
    // viewerRole was actually forwarded through to the lazy flavor.
    expect(screen.getByText(WAITING_KEY)).toBeInTheDocument();
  });
});
