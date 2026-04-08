/**
 * MobileCardLayout Story
 *
 * Covers the mobile mockups in admin-mockups/:
 * - mobile-card-layout-mockup.html (full phone frame with hand sidebar)
 * - mobile-card-entity-types.html (row of all card types)
 */

import { MobileCardLayout, MobileDevicePreview } from '@/components/ui/data-display/meeple-card';
import type { MeepleCardProps } from '@/components/ui/data-display/meeple-card/types';

import type { ShowcaseStory } from '../types';

type Scenario = 'mixed-library' | 'games-only' | 'session-heavy' | 'single';

type MobileCardLayoutShowcaseProps = {
  scenario: string;
  withDevicePreview: boolean;
};

const gameCoverUrl =
  'https://cf.geekdo-images.com/WPKk3MeT3EKhKnhFLB8OoA__itemrep/img/yJB95GXRb10MKzqxKOXGKjgMrPQ=/fit-in/246x300/filters:strip_icc()/pic3490053.jpg';

const mixedLibraryCards: MeepleCardProps[] = [
  {
    id: 'game-catan',
    entity: 'game',
    title: 'Catan',
    subtitle: 'Mayfair Games',
    rating: 7.2,
    ratingMax: 10,
    imageUrl: gameCoverUrl,
  },
  {
    id: 'session-12',
    entity: 'session',
    title: 'Partita #12',
    subtitle: 'Sabato · 4 giocatori',
    status: 'inprogress',
  },
  {
    id: 'player-marco',
    entity: 'player',
    title: 'Marco Rossi',
    subtitle: '32 partite · 18 vittorie',
  },
  {
    id: 'agent-catan',
    entity: 'agent',
    title: 'Catan Expert',
    subtitle: 'RAG Agent',
    status: 'active',
  },
  {
    id: 'kb-rulebook',
    entity: 'kb',
    title: 'Catan Rulebook.pdf',
    subtitle: '42 chunks · Indexed',
    status: 'indexed',
  },
  {
    id: 'event-night',
    entity: 'event',
    title: 'Boardgame Night',
    subtitle: 'Venerdì 20:30',
  },
];

const gamesOnlyCards: MeepleCardProps[] = [
  {
    id: 'game-catan',
    entity: 'game',
    title: 'Catan',
    subtitle: 'Mayfair Games',
    rating: 7.2,
    ratingMax: 10,
    imageUrl: gameCoverUrl,
  },
  {
    id: 'game-wingspan',
    entity: 'game',
    title: 'Wingspan',
    subtitle: 'Stonemaier Games',
    rating: 8.1,
    ratingMax: 10,
  },
  {
    id: 'game-brass',
    entity: 'game',
    title: 'Brass: Birmingham',
    subtitle: 'Roxley Games',
    rating: 8.6,
    ratingMax: 10,
  },
  {
    id: 'game-gloomhaven',
    entity: 'game',
    title: 'Gloomhaven',
    subtitle: 'Cephalofair Games',
    rating: 8.8,
    ratingMax: 10,
  },
];

const sessionHeavyCards: MeepleCardProps[] = [
  {
    id: 'session-12',
    entity: 'session',
    title: 'Partita #12 — Catan',
    subtitle: 'Sabato · 4 giocatori',
    status: 'inprogress',
  },
  {
    id: 'session-11',
    entity: 'session',
    title: 'Partita #11 — Wingspan',
    subtitle: '2h 15m · Completa',
    status: 'completed',
  },
  {
    id: 'session-10',
    entity: 'session',
    title: 'Partita #10 — Brass',
    subtitle: 'In pausa dal 12/03',
    status: 'paused',
  },
  {
    id: 'session-9',
    entity: 'session',
    title: 'Partita #9 — Gloomhaven',
    subtitle: 'Setup completo',
    status: 'setup',
  },
];

const singleCard: MeepleCardProps[] = [
  {
    id: 'game-catan',
    entity: 'game',
    title: 'Catan',
    subtitle: 'Mayfair Games',
    rating: 7.2,
    ratingMax: 10,
    imageUrl: gameCoverUrl,
  },
];

const SCENARIOS: Record<Scenario, MeepleCardProps[]> = {
  'mixed-library': mixedLibraryCards,
  'games-only': gamesOnlyCards,
  'session-heavy': sessionHeavyCards,
  single: singleCard,
};

export const mobileCardLayoutStory: ShowcaseStory<MobileCardLayoutShowcaseProps> = {
  id: 'mobile-card-layout',
  title: 'MobileCardLayout',
  category: 'Data Display',
  description:
    'Mobile layout con hand sidebar, focused card e swipe gesture. Copre i mockup mobile-card-layout-mockup.html e mobile-card-entity-types.html.',

  component: function MobileCardLayoutStory({
    scenario,
    withDevicePreview,
  }: MobileCardLayoutShowcaseProps) {
    const cards = SCENARIOS[scenario as Scenario] ?? mixedLibraryCards;

    const layout = <MobileCardLayout cards={cards} />;

    if (withDevicePreview) {
      return (
        <div className="flex justify-center">
          <MobileDevicePreview>{layout}</MobileDevicePreview>
        </div>
      );
    }

    return (
      <div className="mx-auto flex h-[520px] w-[340px] overflow-hidden rounded-xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)]">
        {layout}
      </div>
    );
  },

  defaultProps: {
    scenario: 'mixed-library',
    withDevicePreview: true,
  },

  controls: {
    scenario: {
      type: 'select',
      label: 'scenario',
      options: ['mixed-library', 'games-only', 'session-heavy', 'single'],
      default: 'mixed-library',
    },
    withDevicePreview: { type: 'boolean', label: 'withDevicePreview', default: true },
  },

  presets: {
    fullMobileMockup: {
      label: 'Full Mobile Mockup',
      props: { scenario: 'mixed-library', withDevicePreview: true },
    },
    entityTypes: {
      label: 'All Entity Types',
      props: { scenario: 'mixed-library', withDevicePreview: false },
    },
    gamesOnly: {
      label: 'Games Only',
      props: { scenario: 'games-only', withDevicePreview: true },
    },
    sessionHeavy: {
      label: 'Session Heavy',
      props: { scenario: 'session-heavy', withDevicePreview: false },
    },
  },
};
