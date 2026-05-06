/**
 * GamebookUploadView unit tests — SP6 Phase C.1.C orchestrator (Issue #789).
 *
 * Coverage (contract §1 §2 §3 §6 §19):
 *   - Default state renders Step 1 (StepIndicator + GameSearchBar)
 *   - URL `?step=2` + `?gameId=` renders Step 2 placeholder
 *   - URL `?step=3` + `?batchId=` renders Step 3 placeholder
 *   - URL `?fixture=step1-no-results` renders NoResultsPanel
 *   - URL `?fixture=step2-denied` renders permission-denied placeholder
 *   - URL `?fixture=step3-progress` renders indexing progress placeholder
 *   - URL `?fixture=step3-cancel-modal` renders cancel modal placeholder
 *   - URL state updates fire router.replace / router.push
 *   - i18n labels resolved via useTranslation (Gate A ICU plural)
 *   - data-slot="gamebook-upload-view" + data-ui-state= cell.kind
 *
 * Mocks: next/navigation router/searchParams/pathname; useTranslation via
 * IntlProvider (mirror Wave D.3 SessionSummaryView.test.tsx pattern).
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import type { ReactElement } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

// ─── next/navigation mocks ────────────────────────────────────────────────

const searchParamsMap: Record<string, string> = {};
const routerPush = vi.fn();
const routerReplace = vi.fn();

vi.mock('next/navigation', () => ({
  useSearchParams: () => ({
    get: (key: string) => searchParamsMap[key] ?? null,
    toString: () => new URLSearchParams(searchParamsMap).toString(),
    [Symbol.iterator]: function* () {
      yield* Object.entries(searchParamsMap);
    },
  }),
  useRouter: () => ({ push: routerPush, replace: routerReplace }),
  usePathname: () => '/gamebook/upload',
}));

// ─── i18n messages (subset matching it.json gamebook.upload.wizard.*) ────

const MESSAGES: Record<string, string> = {
  // stepIndicator
  'gamebook.upload.wizard.stepIndicator.stepGame': 'Gioco',
  'gamebook.upload.wizard.stepIndicator.stepPhoto': 'Foto',
  'gamebook.upload.wizard.stepIndicator.stepIndex': 'Indice',
  // step1
  'gamebook.upload.wizard.step1.searchPlaceholder': 'Cerca un gioco...',
  'gamebook.upload.wizard.step1.tabCatalog': 'Catalogo',
  'gamebook.upload.wizard.step1.tabBgg': 'BGG',
  'gamebook.upload.wizard.step1.bggLoadingTitle': 'Cerco su BoardGameGeek...',
  'gamebook.upload.wizard.step1.bggLoadingSubtitle': 'Può richiedere qualche secondo',
  'gamebook.upload.wizard.step1.noResultsTitle': '"{query}" non trovato',
  'gamebook.upload.wizard.step1.noResultsSubtitle': 'Tre modi per continuare:',
  'gamebook.upload.wizard.step1.actionCardCreate': 'Crea gioco nuovo',
  'gamebook.upload.wizard.step1.actionCardCreateSubtitle':
    'Manuale che non esiste in nessun database',
  'gamebook.upload.wizard.step1.actionCardBgg': 'Cerca su BoardGameGeek',
  'gamebook.upload.wizard.step1.actionCardBggSubtitle': 'Fonte ufficiale con metadati completi',
  'gamebook.upload.wizard.step1.actionCardPrivate': 'Indicizza solo per me',
  'gamebook.upload.wizard.step1.actionCardPrivateSubtitle':
    'Privato — non condiviso con la community',
  // step2
  'gamebook.upload.wizard.step2.title': 'Fotografa il manuale',
  'gamebook.upload.wizard.step2.deniedTitle': 'Camera bloccata',
  'gamebook.upload.wizard.step2.deniedSubtitle':
    'MeepleAI non ha accesso alla fotocamera. Abilita i permessi nelle impostazioni.',
  'gamebook.upload.wizard.step2.unsupportedTitle': 'Fotocamera non supportata',
  'gamebook.upload.wizard.step2.unsupportedSubtitle':
    "Il tuo browser non supporta l'accesso alla fotocamera.",
  // step3
  'gamebook.upload.wizard.step3.progressTitle': 'Indicizzazione…',
  'gamebook.upload.wizard.step3.progressLabel':
    '{processedPages, plural, =0 {0 di {totalPages}} one {1 di {totalPages}} other {{processedPages} di {totalPages}}}',
  'gamebook.upload.wizard.step3.partialTitle': 'Quasi pronto',
  'gamebook.upload.wizard.step3.completeTitle': 'Manuale pronto!',
  'gamebook.upload.wizard.step3.cancelButton': 'Annulla',
  // offline
  'gamebook.upload.wizard.offline.bannerTitle': 'Connessione persa',
  // cancelModal
  'gamebook.upload.wizard.cancelModal.title': "Annullare l'indicizzazione?",
  // common
  'common.selected': 'Selezionato',
};

// ─── Test renderer ────────────────────────────────────────────────────────

import { GamebookUploadView } from '../GamebookUploadView';

function renderView(): ReactElement {
  return render(
    <IntlProvider locale="it" messages={MESSAGES} defaultLocale="it">
      <GamebookUploadView />
    </IntlProvider>
  );
}

beforeEach(() => {
  Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
  routerPush.mockClear();
  routerReplace.mockClear();
});

describe('GamebookUploadView', () => {
  // ── Root rendering ────────────────────────────────────────────────────

  it('renders root data-slot="gamebook-upload-view"', () => {
    renderView();
    expect(document.querySelector('[data-slot="gamebook-upload-view"]')).not.toBeNull();
  });

  it('renders StepIndicator at top', () => {
    renderView();
    expect(document.querySelector('[data-slot="wizard-step-indicator"]')).not.toBeNull();
  });

  // ── Default state (no URL params) ─────────────────────────────────────

  it('renders Step 1 by default with current step = 1', () => {
    renderView();
    const indicator = document.querySelector('[data-slot="wizard-step-indicator"]');
    expect(indicator?.getAttribute('data-current-step')).toBe('1');
  });

  it('renders GameSearchBar in default state', () => {
    renderView();
    expect(document.querySelector('[data-slot="game-search-bar"]')).not.toBeNull();
  });

  it('default state has data-ui-state="step1-default"', () => {
    renderView();
    const root = document.querySelector('[data-slot="gamebook-upload-view"]');
    expect(root?.getAttribute('data-ui-state')).toBe('step1-default');
  });

  it('default state renders empty catalog grid placeholder', () => {
    renderView();
    expect(document.querySelector('[data-slot="catalog-grid-empty"]')).not.toBeNull();
  });

  // ── URL state SSOT: ?step=2 ───────────────────────────────────────────

  it('URL ?step=2 + ?gameId= renders Step 2 placeholder', () => {
    searchParamsMap['step'] = '2';
    searchParamsMap['gameId'] = 'game-123';
    renderView();
    const indicator = document.querySelector('[data-slot="wizard-step-indicator"]');
    expect(indicator?.getAttribute('data-current-step')).toBe('2');
    expect(document.querySelector('[data-slot="step2-placeholder"]')).not.toBeNull();
  });

  it('Step 2 placeholder carries data-game-id', () => {
    searchParamsMap['step'] = '2';
    searchParamsMap['gameId'] = 'game-abc';
    renderView();
    const placeholder = document.querySelector('[data-slot="step2-placeholder"]');
    expect(placeholder?.getAttribute('data-game-id')).toBe('game-abc');
  });

  // ── URL state SSOT: ?step=3 ───────────────────────────────────────────

  it('URL ?step=3 + ?batchId= renders Step 3 placeholder', () => {
    searchParamsMap['step'] = '3';
    searchParamsMap['batchId'] = 'batch-789';
    renderView();
    const indicator = document.querySelector('[data-slot="wizard-step-indicator"]');
    expect(indicator?.getAttribute('data-current-step')).toBe('3');
    expect(document.querySelector('[data-slot="step3-placeholder"]')).not.toBeNull();
  });

  it('Step 3 placeholder carries data-batch-id', () => {
    searchParamsMap['step'] = '3';
    searchParamsMap['batchId'] = 'batch-xyz';
    renderView();
    const placeholder = document.querySelector('[data-slot="step3-placeholder"]');
    expect(placeholder?.getAttribute('data-batch-id')).toBe('batch-xyz');
  });

  // ── Visual fixture override (?fixture=) ───────────────────────────────

  it('?fixture=step1-default renders default catalog grid', () => {
    searchParamsMap['fixture'] = 'step1-default';
    renderView();
    expect(document.querySelector('[data-slot="catalog-grid"]')).not.toBeNull();
  });

  it('?fixture=step1-no-results renders NoResultsPanel', () => {
    searchParamsMap['fixture'] = 'step1-no-results';
    renderView();
    expect(document.querySelector('[data-slot="no-results-panel"]')).not.toBeNull();
  });

  it('?fixture=step1-bgg-loading renders BGG loading spinner', () => {
    searchParamsMap['fixture'] = 'step1-bgg-loading';
    renderView();
    expect(document.querySelector('[data-slot="bgg-loading-spinner"]')).not.toBeNull();
  });

  it('?fixture=step2-ready renders Step 2 placeholder', () => {
    searchParamsMap['fixture'] = 'step2-ready';
    renderView();
    expect(document.querySelector('[data-slot="step2-placeholder"]')).not.toBeNull();
    const root = document.querySelector('[data-slot="gamebook-upload-view"]');
    expect(root?.getAttribute('data-ui-state')).toBe('step2-ready');
  });

  it('?fixture=step2-denied renders permission-denied placeholder', () => {
    searchParamsMap['fixture'] = 'step2-denied';
    renderView();
    const placeholder = document.querySelector('[data-slot="step2-placeholder"]');
    expect(placeholder?.getAttribute('data-cell')).toBe('step2-denied');
    expect(placeholder?.getAttribute('data-permission')).toBe('denied');
  });

  it('?fixture=step3-progress renders indexing placeholder', () => {
    searchParamsMap['fixture'] = 'step3-progress';
    renderView();
    const placeholder = document.querySelector('[data-slot="step3-placeholder"]');
    expect(placeholder).not.toBeNull();
    expect(placeholder?.getAttribute('data-cell')).toBe('step3-progress');
  });

  it('?fixture=step3-complete renders complete placeholder', () => {
    searchParamsMap['fixture'] = 'step3-complete';
    renderView();
    const placeholder = document.querySelector('[data-slot="step3-placeholder"]');
    expect(placeholder?.getAttribute('data-cell')).toBe('step3-complete');
  });

  it('?fixture=step3-cancel-modal renders cancel modal placeholder', () => {
    searchParamsMap['fixture'] = 'step3-cancel-modal';
    renderView();
    expect(document.querySelector('[data-slot="step3-cancel-modal-shell"]')).not.toBeNull();
    expect(document.querySelector('[data-slot="cancel-modal-placeholder"]')).not.toBeNull();
  });

  it('?fixture=step3-offline renders offline placeholder', () => {
    searchParamsMap['fixture'] = 'step3-offline';
    renderView();
    const placeholder = document.querySelector('[data-slot="step3-placeholder"]');
    expect(placeholder?.getAttribute('data-cell')).toBe('step3-offline');
  });

  // ── i18n labels resolved (Gate A) ─────────────────────────────────────

  it('StepIndicator labels resolved via i18n', () => {
    renderView();
    expect(screen.getByText('Gioco')).toBeTruthy();
    expect(screen.getByText('Foto')).toBeTruthy();
    expect(screen.getByText('Indice')).toBeTruthy();
  });

  it('Search placeholder resolved via i18n', () => {
    renderView();
    const input = document.querySelector('input[type="search"]') as HTMLInputElement | null;
    expect(input?.placeholder).toBe('Cerca un gioco...');
  });

  it('Step 3 progress placeholder resolves ICU plural progressLabel', () => {
    searchParamsMap['fixture'] = 'step3-progress';
    renderView();
    // FIXTURE_STEP3_PROGRESS has processedPages=7, totalPages=12 → "7 di 12"
    expect(screen.getByText(/7 di 12/)).toBeTruthy();
  });

  // ── URL mutation handlers ─────────────────────────────────────────────

  it('typing into search input fires router.replace with ?q=', () => {
    renderView();
    const input = document.querySelector('input[type="search"]') as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'gloomhaven' } });
    expect(routerReplace).toHaveBeenCalled();
    const lastCall = routerReplace.mock.calls[routerReplace.mock.calls.length - 1][0];
    expect(lastCall).toContain('q=gloomhaven');
  });

  it('clicking BGG tab fires router.replace with ?tab=bgg', () => {
    renderView();
    const bggTab = document.querySelector(
      '[data-slot="game-search-tab"][data-tab-key="bgg"]'
    ) as HTMLButtonElement | null;
    expect(bggTab).not.toBeNull();
    fireEvent.click(bggTab as HTMLButtonElement);
    expect(routerReplace).toHaveBeenCalled();
    const lastCall = routerReplace.mock.calls[routerReplace.mock.calls.length - 1][0];
    expect(lastCall).toContain('tab=bgg');
  });

  it('selecting a game card fires router.push with ?step=2 + ?gameId=', () => {
    searchParamsMap['fixture'] = 'step1-default';
    renderView();
    const firstCard = document.querySelector(
      '[data-slot="game-search-card"]'
    ) as HTMLButtonElement | null;
    expect(firstCard).not.toBeNull();
    fireEvent.click(firstCard as HTMLButtonElement);
    expect(routerPush).toHaveBeenCalled();
    const lastCall = routerPush.mock.calls[routerPush.mock.calls.length - 1][0];
    expect(lastCall).toContain('step=2');
    expect(lastCall).toContain('gameId=');
  });

  it('NoResultsPanel "Cerca su BGG" action fires router.replace with ?tab=bgg', () => {
    searchParamsMap['fixture'] = 'step1-no-results';
    renderView();
    // Click the second ActionCard ("Cerca su BoardGameGeek")
    const bggCard = screen.getByText('Cerca su BoardGameGeek');
    fireEvent.click(bggCard);
    expect(routerReplace).toHaveBeenCalled();
    const lastCall = routerReplace.mock.calls[routerReplace.mock.calls.length - 1][0];
    expect(lastCall).toContain('tab=bgg');
  });
});
