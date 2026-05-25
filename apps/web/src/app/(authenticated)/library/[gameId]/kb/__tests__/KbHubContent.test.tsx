/**
 * KbHubContent orchestrator tests (Issue #1481).
 *
 * Validates loading/error/empty/default states + delete + reindex flow wiring.
 * Hooks are mocked to isolate orchestrator FSM and i18n label resolution.
 * P71: MESSAGES subset covers all new `pages.library.gameDetail.kb.*` keys.
 */

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { KbHubContent } from '../_content';

// ─── Hook mocks ──────────────────────────────────────────
const mockUseUserKbStatus = vi.fn();
const mockUseGamePdfs = vi.fn();
const mockUseReindexKb = vi.fn();
const mockUseRebuildRaptor = vi.fn();
const mockUseDeletePdf = vi.fn();

vi.mock('@/hooks/queries/useKbHub', () => ({
  useUserKbStatus: (gameId?: string) => mockUseUserKbStatus(gameId),
  useGamePdfs: (gameId?: string) => mockUseGamePdfs(gameId),
  useReindexKb: (gameId: string) => mockUseReindexKb(gameId),
  useRebuildRaptor: (gameId: string) => mockUseRebuildRaptor(gameId),
  useDeletePdf: (gameId: string) => mockUseDeletePdf(gameId),
}));

// Mock useTranslation with a lightweight lookup in MESSAGES dict (defined below).
// Avoids react-intl IntlProvider initialization that becomes pathological under the
// scale of orchestrator label assembly (~90 t() calls per render).
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string, values?: Record<string, unknown>) => {
      const template = MESSAGES[key] ?? key;
      if (!values) return template;
      let result = template;
      for (const [k, v] of Object.entries(values)) {
        result = result.replace(`{${k}}`, String(v));
      }
      return result;
    },
    formatMessage: ({ id }: { id: string }) => MESSAGES[id] ?? id,
    locale: 'it',
    formatNumber: (n: number) => String(n),
    formatDate: (d: Date) => d.toISOString(),
    formatTime: (d: Date) => d.toISOString(),
    formatRelativeTime: (n: number) => String(n),
  }),
  FormattedMessage: () => null,
  FormattedDate: () => null,
  FormattedTime: () => null,
  FormattedNumber: () => null,
}));

// ─── MESSAGES (P71): mirror src/locales/it.json pages.library.gameDetail.kb.* ─
const MESSAGES: Record<string, string> = {
  'pages.library.gameDetail.kb.empty.title': 'Nessun PDF indicizzato',
  'pages.library.gameDetail.kb.empty.description': 'Carica il primo documento PDF per {gameTitle}.',
  'pages.library.gameDetail.kb.empty.ctaLabel': 'Carica primo documento',
  'pages.library.gameDetail.kb.empty.supportedFormats': 'Formati: PDF, max 200 MB',
  'pages.library.gameDetail.kb.header.titleSuffix': 'Knowledge Base',
  'pages.library.gameDetail.kb.header.uploadCta': '+ Carica PDF',
  'pages.library.gameDetail.kb.header.reindexAllCta': 'Re-index all',
  'pages.library.gameDetail.kb.stats.docs': '{count} documenti',
  'pages.library.gameDetail.kb.stats.docsLabel': 'Documenti',
  'pages.library.gameDetail.kb.stats.chunksLabel': 'Chunks',
  'pages.library.gameDetail.kb.stats.chunksTemplate': '{count} chunks',
  'pages.library.gameDetail.kb.stats.embeddingsLabel': 'Embeddings',
  'pages.library.gameDetail.kb.stats.embeddingsTemplate': '{count} embeddings',
  'pages.library.gameDetail.kb.stats.lastReindexLabel': 'Ultima idx.',
  'pages.library.gameDetail.kb.stats.lastReindexTemplate': 'ultima reindex {relative}',
  'pages.library.gameDetail.kb.stats.raptorLabel': 'RAPTOR last',
  'pages.library.gameDetail.kb.stats.coverageLabel': 'Copertura',
  'pages.library.gameDetail.kb.stats.coverageStripLabel': 'Copertura: {level}',
  'pages.library.gameDetail.kb.stats.coverage.None': 'Nessuna',
  'pages.library.gameDetail.kb.stats.coverage.Basic': 'Base',
  'pages.library.gameDetail.kb.stats.coverage.Standard': 'Standard',
  'pages.library.gameDetail.kb.stats.coverage.Complete': 'Completa',
  'pages.library.gameDetail.kb.stats.cardTitle': 'KB Coverage Stats',
  'pages.library.gameDetail.kb.stats.cardSubtitle': 'Metriche',
  'pages.library.gameDetail.kb.stats.lifetimeCostLabel': 'Costo lifetime',
  'pages.library.gameDetail.kb.stats.sparklineLabel': 'Consumo',
  'pages.library.gameDetail.kb.stats.sparklineStart': '-7gg',
  'pages.library.gameDetail.kb.stats.sparklineEnd': 'oggi',
  'pages.library.gameDetail.kb.stats.columnHeaders.document': 'Documento',
  'pages.library.gameDetail.kb.stats.columnHeaders.status': 'Stato',
  'pages.library.gameDetail.kb.stats.columnHeaders.uploaded': 'Caricato',
  'pages.library.gameDetail.kb.pdfRow.openCta': 'Apri',
  'pages.library.gameDetail.kb.pdfRow.openAria': 'Apri {pdfName}',
  'pages.library.gameDetail.kb.pdfRow.chunksTemplate': '{count} chunks',
  'pages.library.gameDetail.kb.pdfRow.status.ready': 'Ready',
  'pages.library.gameDetail.kb.pdfRow.status.indexing': 'Indexing',
  'pages.library.gameDetail.kb.pdfRow.status.stale': 'Stale',
  'pages.library.gameDetail.kb.pdfRow.status.failed': 'Failed',
  'pages.library.gameDetail.kb.actionsMenu.headerSubtitle': '{size} · {date}',
  'pages.library.gameDetail.kb.actionsMenu.open.label': 'Apri dettaglio',
  'pages.library.gameDetail.kb.actionsMenu.open.description': 'Vedi chunks',
  'pages.library.gameDetail.kb.actionsMenu.open.icon': '↗',
  'pages.library.gameDetail.kb.actionsMenu.reindex.label': 'Re-index',
  'pages.library.gameDetail.kb.actionsMenu.reindex.description': 'Rielabora',
  'pages.library.gameDetail.kb.actionsMenu.reindex.icon': '⟳',
  'pages.library.gameDetail.kb.actionsMenu.cost.label': 'Costo',
  'pages.library.gameDetail.kb.actionsMenu.cost.description': 'Token',
  'pages.library.gameDetail.kb.actionsMenu.cost.icon': '📋',
  'pages.library.gameDetail.kb.actionsMenu.move.label': 'Sposta',
  'pages.library.gameDetail.kb.actionsMenu.move.description': 'Altra KB',
  'pages.library.gameDetail.kb.actionsMenu.move.icon': '📦',
  'pages.library.gameDetail.kb.actionsMenu.delete.label': 'Elimina',
  'pages.library.gameDetail.kb.actionsMenu.delete.description': 'Rimuovi',
  'pages.library.gameDetail.kb.actionsMenu.delete.icon': '🗑',
  'pages.library.gameDetail.kb.reindexModal.title': 'Re-index full KB',
  'pages.library.gameDetail.kb.reindexModal.subtitle': '{gameTitle} · {docCount} documenti',
  'pages.library.gameDetail.kb.reindexModal.costHeader': 'Stima costo',
  'pages.library.gameDetail.kb.reindexModal.description': 'Rielabora tutto.',
  'pages.library.gameDetail.kb.reindexModal.reindexCta': 'Re-index',
  'pages.library.gameDetail.kb.reindexModal.cancelCta': 'Annulla',
  'pages.library.gameDetail.kb.reindexModal.runningTitle': 'In corso',
  'pages.library.gameDetail.kb.reindexModal.jobIdLabel': 'Job ID',
  'pages.library.gameDetail.kb.reindexModal.progressTemplate': '{processed} / {total} chunks',
  'pages.library.gameDetail.kb.reindexModal.doneTitle': 'Completato',
  'pages.library.gameDetail.kb.reindexModal.doneSummaryTemplate':
    '{chunks} chunks · {embeddings} embeddings · costo {cost}',
  'pages.library.gameDetail.kb.reindexModal.closeCta': 'Chiudi',
  'pages.library.gameDetail.kb.raptor.title': 'RAPTOR Rebuild',
  'pages.library.gameDetail.kb.raptor.description': 'RAPTOR descrizione',
  'pages.library.gameDetail.kb.raptor.lockedBadge': 'PRO',
  'pages.library.gameDetail.kb.raptor.activeBadge': 'PRO ✓',
  'pages.library.gameDetail.kb.raptor.lockedNote': 'Richiede Pro.',
  'pages.library.gameDetail.kb.raptor.upgradeCta': 'Upgrade',
  'pages.library.gameDetail.kb.raptor.upgradeLink': 'Scopri Pro',
  'pages.library.gameDetail.kb.raptor.rebuildCta': 'Rebuild',
  'pages.library.gameDetail.kb.raptor.metrics.lastRebuild': 'Ultimo',
  'pages.library.gameDetail.kb.raptor.metrics.summaries': 'Summaries',
  'pages.library.gameDetail.kb.raptor.estimateLabel': 'Stima',
  'pages.library.gameDetail.kb.raptor.estimateDescription': '~{chunks} chunks',
  'pages.library.gameDetail.kb.delete.title': 'Confermi eliminazione PDF?',
  'pages.library.gameDetail.kb.delete.subtitlePrefix': 'PDF:',
  'pages.library.gameDetail.kb.delete.listHeader': 'Sarà eliminato:',
  'pages.library.gameDetail.kb.delete.warning': 'Irreversibile',
  'pages.library.gameDetail.kb.delete.deleteCta': 'Elimina',
  'pages.library.gameDetail.kb.delete.cancelCta': 'Annulla',
  'pages.library.gameDetail.kb.errors.loadFailed': 'Caricamento fallito',
  'pages.library.gameDetail.kb.errors.reindexFailed': 'Reindex fallito',
  'pages.library.gameDetail.kb.errors.raptorFailed': 'Raptor fallito',
  'pages.library.gameDetail.kb.errors.deleteFailed': 'Delete fallito',
};

function renderWithIntl(node: ReactNode): ReturnType<typeof render> {
  return render(<>{node}</>);
}

const GAME_ID = '11111111-1111-1111-1111-111111111111';

function pdfFixture(overrides: Partial<{ id: string; name: string; bytes: number }> = {}): {
  id: string;
  name: string;
  pageCount: number;
  fileSizeBytes: number;
  uploadedAt: string;
  source: 'Custom';
} {
  return {
    id: overrides.id ?? 'p1',
    name: overrides.name ?? 'Rulebook v2',
    pageCount: 50,
    fileSizeBytes: overrides.bytes ?? 47185920,
    uploadedAt: '2026-05-20T00:00:00Z',
    source: 'Custom',
  };
}

describe('KbHubContent orchestrator (Issue #1481)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseReindexKb.mockReturnValue({ mutateAsync: vi.fn() });
    mockUseRebuildRaptor.mockReturnValue({ mutateAsync: vi.fn() });
    mockUseDeletePdf.mockReturnValue({ mutateAsync: vi.fn() });
  });

  it('renders skeleton while either query is loading', () => {
    mockUseUserKbStatus.mockReturnValue({ isLoading: true, data: null, isError: false });
    mockUseGamePdfs.mockReturnValue({ isLoading: true, data: [], isError: false });
    const { container } = renderWithIntl(<KbHubContent gameId={GAME_ID} />);
    expect(container.querySelector('[data-slot="kb-hub-skeleton"]')).toBeInTheDocument();
  });

  it('renders error alert when status query errors', () => {
    mockUseUserKbStatus.mockReturnValue({
      isLoading: false,
      isError: true,
      data: null,
    });
    mockUseGamePdfs.mockReturnValue({ isLoading: false, isError: false, data: [] });
    const { container } = renderWithIntl(<KbHubContent gameId={GAME_ID} />);
    expect(container.querySelector('[data-slot="kb-hub-error"]')).toBeInTheDocument();
    expect(screen.getByText('Caricamento fallito')).toBeInTheDocument();
  });

  it('renders EmptyState when no PDFs', () => {
    mockUseUserKbStatus.mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        gameId: GAME_ID,
        isIndexed: false,
        documentCount: 0,
        coverageScore: 0,
        coverageLevel: 'None',
        suggestedQuestions: [],
      },
    });
    mockUseGamePdfs.mockReturnValue({ isLoading: false, isError: false, data: [] });
    const { container } = renderWithIntl(<KbHubContent gameId={GAME_ID} />);
    expect(container.querySelector('[data-slot="kb-hub-empty-state"]')).toBeInTheDocument();
  });

  it('renders HubDefault + KbStatsCard + RaptorPanel when PDFs present', () => {
    mockUseUserKbStatus.mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        gameId: GAME_ID,
        isIndexed: true,
        documentCount: 2,
        coverageScore: 70,
        coverageLevel: 'Standard',
        suggestedQuestions: [],
      },
    });
    mockUseGamePdfs.mockReturnValue({
      isLoading: false,
      isError: false,
      data: [pdfFixture({ id: 'p1' }), pdfFixture({ id: 'p2', name: 'Scenario Book' })],
    });
    const { container } = renderWithIntl(<KbHubContent gameId={GAME_ID} />);
    expect(container.querySelector('[data-slot="kb-hub-hub-default"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="kb-hub-stats-card"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="kb-hub-raptor-panel"]')).toBeInTheDocument();
    expect(container.querySelectorAll('[data-slot="kb-hub-pdf-row"]')).toHaveLength(2);
  });

  it('opens reindex modal when reindex-all CTA clicked', () => {
    mockUseUserKbStatus.mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        gameId: GAME_ID,
        isIndexed: true,
        documentCount: 1,
        coverageScore: 50,
        coverageLevel: 'Basic',
        suggestedQuestions: [],
      },
    });
    mockUseGamePdfs.mockReturnValue({
      isLoading: false,
      isError: false,
      data: [pdfFixture()],
    });
    renderWithIntl(<KbHubContent gameId={GAME_ID} />);
    expect(document.querySelector('[data-slot="kb-hub-reindex-modal"]')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Re-index all' }));
    expect(document.querySelector('[data-slot="kb-hub-reindex-modal"]')).toBeInTheDocument();
  });

  it('triggers delete mutation when DeleteDialog confirmed', async () => {
    const deleteMutateAsync = vi.fn().mockResolvedValue(undefined);
    mockUseDeletePdf.mockReturnValue({ mutateAsync: deleteMutateAsync });
    mockUseUserKbStatus.mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        gameId: GAME_ID,
        isIndexed: true,
        documentCount: 1,
        coverageScore: 50,
        coverageLevel: 'Basic',
        suggestedQuestions: [],
      },
    });
    mockUseGamePdfs.mockReturnValue({
      isLoading: false,
      isError: false,
      data: [pdfFixture({ id: 'pdf-to-delete' })],
    });
    renderWithIntl(<KbHubContent gameId={GAME_ID} />);
    // Open actions menu by clicking the pdf row CTA
    fireEvent.click(screen.getByRole('button', { name: /Apri Rulebook v2/i }));
    // ActionsMenu open — click delete action via document (portal)
    const deleteAction = document.querySelector(
      '[data-slot="kb-hub-actions-menu-delete"]'
    ) as HTMLElement | null;
    expect(deleteAction).toBeInTheDocument();
    if (deleteAction) fireEvent.click(deleteAction);
    // DeleteDialog open — confirm
    const confirmBtn = document.querySelector(
      '[data-slot="kb-hub-delete-confirm"]'
    ) as HTMLButtonElement | null;
    expect(confirmBtn).toBeInTheDocument();
    if (confirmBtn) fireEvent.click(confirmBtn);
    await waitFor(() => expect(deleteMutateAsync).toHaveBeenCalledWith('pdf-to-delete'));
  });
});
