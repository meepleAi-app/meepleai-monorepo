/**
 * @mockup admin-mockups/design_files/librogame-runthrough-game-detail.html
 *
 * LibroGameDetailView argTypes matrix story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * State-driving strategy (P-Template):
 *   LibroGameDetailView is props-driven (`gameDetail: LibraryGameDetail`).
 *   The component itself has no loading/error/notFound prop — those states are
 *   handled by the parent page shell (`/library/[gameId]/page.tsx`).
 *
 *   Frame01_Default  — real component, gameTitle='Nanolith' (isLibroGame allowlist)
 *                      so NanolithCampaignCTA renders "Avvia libro game" CTA.
 *   Frame02_Loading  — presentational mock (LibroGameDetailLoadingMock) — skeleton
 *                      hero + meta + CTA placeholder, forward-refactor.
 *   Frame03_Error    — presentational mock (LibroGameDetailErrorMock) — error card.
 *   Frame04_NotFound — presentational mock (LibroGameDetailNotFoundMock) — not-in-
 *                      collection card.
 *
 * NanolithCampaignCTA dep notes:
 *   - isLibroGame() checks gameTitle against Set {'Nanolith'}. CTA renders only
 *     when title matches → fixture uses 'Nanolith'.
 *   - CampaignSetupDrawer inside NanolithCampaignCTA uses useMutation
 *     (@tanstack/react-query) and useRouter (next/navigation). Both are covered
 *     by the global preview.tsx decorators (QueryClientProvider + nextjs adapter).
 *   - The drawer POST /api/v1/gamebook/campaigns fires only on step-3 submit,
 *     not on render — no additional MSW handler needed for visual stories.
 *
 * Stage axis (librogame-runthrough-game-detail.html — 4 states):
 *   01 default  — game loaded, CTA prominent, Info tab, KB ready
 *   02 loading  — skeleton hero + meta + CTA (mock)
 *   03 error    — network error card + retry (mock)
 *   04 not-found — not in collection, add-to-catalog CTA (mock)
 *
 * Refs: spec docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import { fn } from 'storybook/test';

import {
  LibroGameDetailErrorMock,
  LibroGameDetailLoadingMock,
  LibroGameDetailNotFoundMock,
} from '@/__tests__/fixtures/mockup-pilots/librogame/_mocks/LibroGameDetailMocks';
import {
  MOCK_LIBROGAME_GAME_DETAIL,
  MOCK_LIBROGAME_GAME_DETAIL_KB_ERROR,
  MOCK_LIBROGAME_GAME_DETAIL_KB_INDEXING,
} from '@/__tests__/fixtures/mockup-pilots/librogame/librogame-game-detail';

import { LibroGameDetailView } from './LibroGameDetailView';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof LibroGameDetailView> = {
  title: 'Pages/Librogame/Game Detail',
  component: LibroGameDetailView,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/library/game-nanolith-001',
        query: {},
      },
    },
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di librogame-runthrough-game-detail.html 4 stati. Hero usa ' +
          '`LibroGameDetailView` (props-driven) per il default; stati loading/error/not-found ' +
          'sono presentational mock del page-shell parent. `NanolithCampaignCTA` richiede ' +
          "gameTitle='Nanolith' (isLibroGame allowlist) per rendere il CTA primario.",
      },
    },
  },
  argTypes: {
    gameDetail: {
      control: 'object',
      description: 'Full LibraryGameDetail shape — drives hero card, meta grid, pip bar, KB panel.',
    },
  },
  args: {
    gameDetail: MOCK_LIBROGAME_GAME_DETAIL,
  },
};
export default meta;

type Story = StoryObj<typeof LibroGameDetailView>;

// ── Frame 01 · Default — game loaded, CTA + Info tab ──────────────────────

export const Frame01_Default: Story = {
  name: '01 · Default — game caricato, CTA "Avvia libro game", Info tab, KB ready',
  args: {
    gameDetail: MOCK_LIBROGAME_GAME_DETAIL,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Stato principale (Stato 1 mockup). Aaron loggato, Nanolith in collezione, ' +
          'KB ready 142 chunk. NanolithCampaignCTA visibile above-the-fold con CTA ' +
          '"📖 Avvia libro game". Info tab attivo con descrizione + KB status card.',
      },
    },
  },
};

// ── Frame 01b · Default KB Indexing variant ─────────────────────────────────

export const Frame01b_KbIndexing: Story = {
  name: '01b · Default — KB indexing in corso (kbStatus: indexing)',
  args: {
    gameDetail: MOCK_LIBROGAME_GAME_DETAIL_KB_INDEXING,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Variant di Frame01 con kbStatus=indexing: KB badge mostra "Indicizzazione in corso…". ' +
          'CTA comunque visibile (NanolithCampaignCTA non dipende da kbStatus).',
      },
    },
  },
};

// ── Frame 01c · Default KB Error variant ────────────────────────────────────

export const Frame01c_KbError: Story = {
  name: '01c · Default — KB error (kbStatus: error)',
  args: {
    gameDetail: MOCK_LIBROGAME_GAME_DETAIL_KB_ERROR,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Variant di Frame01 con kbStatus=error: KB badge mostra "Errore durante l\'indicizzazione". ' +
          "Replica edge case dell'Info panel quando un PDF ha fallito il processing.",
      },
    },
  },
};

// ── Frame 02 · Loading — skeleton (presentational mock) ────────────────────

export const Frame02_Loading: StoryObj<typeof LibroGameDetailLoadingMock> = {
  name: '02 · Loading — skeleton hero + meta + CTA (forward-refactor mock)',
  render: () => <LibroGameDetailLoadingMock />,
  parameters: {
    docs: {
      description: {
        story:
          'Stato 2 mockup. Skeleton hero (aspect 12/4) + 4-cell meta grid + pip bar + ' +
          'CTA placeholder. Rispetta animate-pulse (CSS Tailwind). ' +
          'forward-refactor: sostituire con il vero skeleton primitive quando estratto ' +
          "dal page shell '/library/[gameId]/page.tsx'.",
      },
    },
  },
};

// ── Frame 03 · Error — network error card (presentational mock) ─────────────

export const Frame03_Error: StoryObj<typeof LibroGameDetailErrorMock> = {
  name: '03 · Error — errore rete, retry CTA (forward-refactor mock)',
  render: () => <LibroGameDetailErrorMock onRetry={fn()} onBack={fn()} />,
  parameters: {
    docs: {
      description: {
        story:
          'Stato 3 mockup. Error card con ⚠️ + "Impossibile caricare il gioco" + ' +
          'bottoni "← Libreria" e "🔄 Riprova". ' +
          'forward-refactor: sostituire con il vero error primitive quando estratto ' +
          "dal page shell '/library/[gameId]/page.tsx'.",
      },
    },
  },
};

// ── Frame 04 · Not Found — not in collection (presentational mock) ───────────

export const Frame04_NotFound: StoryObj<typeof LibroGameDetailNotFoundMock> = {
  name: '04 · Not Found — gioco non in collezione, add-to-catalog CTA (forward-refactor mock)',
  render: () => (
    <LibroGameDetailNotFoundMock gameTitle="Nanolith" onBack={fn()} onAddToCatalog={fn()} />
  ),
  parameters: {
    docs: {
      description: {
        story:
          'Stato 4 mockup. Edge case Phase A: precondizione mancante (Nanolith non in ' +
          'collection). Card 🎲 + "non in collezione" + bottoni "← Libreria" e ' +
          '"+ Aggiungi al catalogo". ' +
          'forward-refactor: sostituire con il vero not-found primitive quando estratto ' +
          "dal page shell '/library/[gameId]/page.tsx'.",
      },
    },
  },
};
