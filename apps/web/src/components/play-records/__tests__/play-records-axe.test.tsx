/**
 * play-records — axe AA gate (#2348 + #2349 + #2350).
 *
 * Tests three rendering contexts:
 *   1. SessionCreateForm (create mode, step 0) — DEC-A5 T6 / #2348
 *   2. StatisticsView (empty state)            — DEC-A5 T10 / #2350
 *   3. PlayRecordDetailView (won fixture)       — DEC-A5 T8 / #2349
 *
 * Provider strategy: mirror each component's existing test file exactly.
 *   - SessionCreateForm: QueryClientProvider + vi.mock for i18n/navigation/store/media/combobox
 *   - StatisticsView: vi.mock for usePlayerStatistics/useSharedGames/i18n/navigation (no QC needed)
 *   - PlayRecordDetailView: QueryClientProvider + IntlProvider + vi.mock for navigation/useCurrentUser
 *     (MSW global setup + resetPlayRecordsState ensures pr-won-1 is in the in-memory store)
 *
 * No new provider stack is introduced — all patterns are copied verbatim from the
 * respective sibling test files in this directory and in stats/__tests__/page.test.tsx.
 */
import { render } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { IntlProvider } from 'react-intl';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import {
  FIXTURE_WON,
  resetPlayRecordsState,
} from '../../../__tests__/mocks/handlers/play-records.handlers';
import { SessionCreateForm } from '../SessionCreateForm';
import { StatisticsView } from '../StatisticsView';
import { PlayRecordDetailView } from '../PlayRecordDetailView';
import { mockPlayerStatisticsEmpty } from '../../play-records/stats/__tests__/fixtures';

expect.extend(toHaveNoViolations);

// ─── Mocks for SessionCreateForm ──────────────────────────────────────────────

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) => key,
  }),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn(), refresh: vi.fn() }),
  usePathname: () => '/',
  useParams: () => ({ id: 'pr-won-1' }),
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/lib/hooks/useMediaQuery', () => ({
  useMediaQuery: () => true, // mobile: single-column layout (simpler DOM for axe)
}));

vi.mock('@/lib/stores/play-records-store', () => ({
  usePlayRecordsStore: () => ({
    sessionCreation: { currentStep: 0 },
    nextStep: vi.fn(),
    prevStep: vi.fn(),
    resetSessionCreation: vi.fn(),
  }),
}));

vi.mock('@/components/play-records/GameCombobox', () => ({
  GameCombobox: ({
    placeholder,
  }: {
    value?: string;
    onSelect: (id: string, name: string) => void;
    onNotFound?: () => void;
    disabled?: boolean;
    placeholder?: string;
  }) => (
    <div>
      <input
        data-testid="game-combobox"
        placeholder={placeholder ?? 'Search game'}
        aria-label={placeholder ?? 'Search game'}
        readOnly
      />
    </div>
  ),
}));

vi.mock('@/components/library/AddPrivateGameForm', () => ({
  AddPrivateGameForm: ({
    onCancel,
  }: {
    onSubmit: () => void;
    onCancel: () => void;
    isSubmitting?: boolean;
  }) => (
    <button type="button" onClick={onCancel}>
      Cancel Add Game
    </button>
  ),
}));

// ─── Mocks for StatisticsView ─────────────────────────────────────────────────

vi.mock('@/lib/domain-hooks/usePlayRecords', () => ({
  usePlayerStatistics: vi.fn(() => ({
    data: mockPlayerStatisticsEmpty,
    isLoading: false,
    error: null,
  })),
  useCreatePlayRecord: vi.fn(() => ({ mutateAsync: vi.fn(), isPending: false })),
  useUpdatePlayRecord: vi.fn(() => ({ mutateAsync: vi.fn(), isPending: false })),
  useDeletePlayRecord: vi.fn(() => ({ mutateAsync: vi.fn(), isPending: false })),
  usePlayRecord: vi.fn(() => ({
    data: FIXTURE_WON,
    isLoading: false,
    error: null,
  })),
  usePlayRecords: vi.fn(() => ({ data: undefined, isLoading: false, error: null })),
}));

vi.mock('@/lib/play-records/useSharedGames', () => ({
  useSharedGames: vi.fn(() => ({
    data: new Map(),
    isLoading: false,
    error: null,
  })),
}));

// ─── Mocks for PlayRecordDetailView ───────────────────────────────────────────

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({
    data: { id: 'user-me', email: 'test@test.com', role: 'user' },
    isLoading: false,
  }),
  userKeys: { current: () => ['user', 'current'] },
}));

// ─── i18n messages for PlayRecordDetailView ───────────────────────────────────

const MESSAGES: Record<string, string> = {
  'playRecords.detail.hero.won': 'Vittoria',
  'playRecords.detail.hero.tied': 'Pareggio',
  'playRecords.detail.hero.cooperative': 'Cooperativa',
  'playRecords.detail.hero.inprogress': 'In corso',
  'playRecords.detail.hero.planned': 'Pianificata',
  'playRecords.detail.hero.bannerWon': '{winnerName} vince {gameName}',
  'playRecords.detail.hero.bannerTied': 'Pareggio a {score} punti',
  'playRecords.detail.hero.bannerCooperative': 'Partita cooperativa di {gameName}',
  'playRecords.detail.hero.bannerInProgress': '{gameName} in corso',
  'playRecords.detail.hero.bannerPlanned': '{gameName} pianificata',
  'playRecords.detail.hero.metaPlayers': '{count} giocatori',
  'playRecords.detail.hero.ctaStart': 'Avvia partita',
  'playRecords.detail.connectionBar.label': 'Collegamenti',
  'playRecords.detail.connectionBar.playerCount': '{count} giocatori',
  'playRecords.detail.connectionBar.chatCount': '{count} messaggi',
  'playRecords.detail.connectionBar.noChat': 'Nessuna chat',
  'playRecords.detail.kpi.title': 'Statistiche',
  'playRecords.detail.kpi.duration': 'Durata',
  'playRecords.detail.kpi.topScore': 'Top',
  'playRecords.detail.kpi.avg': 'Media',
  'playRecords.detail.kpi.spread': 'Distacco',
  'playRecords.detail.classifica.title': 'Classifica',
  'playRecords.detail.classifica.winnerBadge': 'Vincitore',
  'playRecords.detail.scoreBreakdown.title': 'Dettaglio',
  'playRecords.detail.scoreBreakdown.toggleLabel': 'Mostra dettaglio',
  'playRecords.detail.loading': 'Caricamento...',
  'playRecords.detail.notFound': 'Non trovata',
  'playRecords.detail.retry': 'Riprova',
  'playRecords.detail.backToList': 'Torna alle partite',
};

// ─── Helpers ──────────────────────────────────────────────────────────────────

function makeQueryClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function withQueryClient(ui: React.ReactElement) {
  const qc = makeQueryClient();
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

function withQueryClientAndIntl(ui: React.ReactElement) {
  const qc = makeQueryClient();
  return render(
    <QueryClientProvider client={qc}>
      <IntlProvider locale="it" messages={MESSAGES}>
        {ui}
      </IntlProvider>
    </QueryClientProvider>
  );
}

// ─── Tests ────────────────────────────────────────────────────────────────────

beforeEach(() => {
  resetPlayRecordsState();
});

describe('play-records — axe AA gate', () => {
  it('SessionCreateForm (create, step 0) — no violations', async () => {
    const { container } = withQueryClient(
      <SessionCreateForm onSubmit={() => {}} onCancel={() => {}} />
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it('StatisticsView (empty) — no violations', async () => {
    // StatisticsView uses mocked usePlayerStatistics (empty fixture above).
    // No QueryClientProvider needed: all data hooks are vi.mock'd.
    const { container } = render(<StatisticsView />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it('PlayRecordDetailView (won) — no violations', async () => {
    // PlayRecordDetailView uses usePlayRecord (mocked above → FIXTURE_WON).
    // IntlProvider required for react-intl message lookup inside the component tree.
    const { container } = withQueryClientAndIntl(
      <PlayRecordDetailView recordId={FIXTURE_WON.id} />
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
