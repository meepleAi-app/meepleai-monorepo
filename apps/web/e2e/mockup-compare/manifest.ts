/**
 * Mockup↔live compare — pairing manifest (#2999).
 *
 * Ogni entry accoppia un page-mock HTML statico (admin-mockups/design_files)
 * con la route live reale. La capture spec screenshotta entrambi.
 * Estensione: aggiungi righe qui (+ un `mock` page.route se la route non
 * supporta il seam `?fixture=`).
 *
 * 🔴 URL host-agnostici: l'app usa URL RELATIVI nel browser
 * (getApiBase()→'', httpClient.ts:51-55) → richieste a localhost:3000/api/...
 * via proxy Next. I mock page.route DEVONO usare glob **\/api/v1/... , MAI
 * URL assoluti localhost:8080 (non intercetterebbero).
 */
import path from 'node:path';

import type { Page } from '@playwright/test';

export interface MockupComparePair {
  /** Slug stabile (kebab-case) — usato nei nomi file e nella gallery. */
  readonly id: string;
  /** Titolo umano mostrato nella gallery. */
  readonly label: string;
  /** Nome file HTML dentro admin-mockups/design_files/. */
  readonly mockupHtml: string;
  /** Route live (path relativo, es. "/library/wishlist"). */
  readonly route: string;
  /** Ruolo auth per il bypass E2E. Default 'user'. */
  readonly auth?: 'user' | 'admin';
  /** Setup page.route opzionale per mockare le API della route (glob host-agnostici). */
  readonly mock?: (page: Page) => Promise<void>;
  /** Viewport override. Default 1920x1080. */
  readonly viewport?: { readonly width: number; readonly height: number };
}

/** apps/web/e2e/mockup-compare → repo root → admin-mockups/design_files. */
export const DESIGN_FILES_DIR = path.resolve(
  process.cwd(),
  '..',
  '..',
  'admin-mockups',
  'design_files'
);

/** apps/web/mockup-compare-output (gitignored). */
export const OUTPUT_DIR = path.resolve(process.cwd(), 'mockup-compare-output');

/** Wishlist fixture — WishlistItemDto[] con gameName inline (no library map). */
const WISHLIST_FIXTURE = [
  {
    id: '11111111-1111-4111-8111-111111111111',
    userId: '99999999-9999-4999-8999-999999999999',
    gameId: '22222222-2222-4222-8222-222222222222',
    gameName: 'Terraforming Mars',
    priority: 'high',
    targetPrice: 45.0,
    notes: 'Aspetto un saldo sotto i 50€',
    addedAt: '2026-07-01T10:00:00.000Z',
    updatedAt: null,
    visibility: 'private',
  },
  {
    id: '33333333-3333-4333-8333-333333333333',
    userId: '99999999-9999-4999-8999-999999999999',
    gameId: '44444444-4444-4444-8444-444444444444',
    gameName: 'Wingspan',
    priority: 'medium',
    targetPrice: null,
    notes: null,
    addedAt: '2026-06-15T09:30:00.000Z',
    updatedAt: '2026-06-20T12:00:00.000Z',
    visibility: 'private',
  },
];

export const PAIRS: readonly MockupComparePair[] = [
  {
    id: 'library-wishlist',
    label: 'Library · Wishlist',
    mockupHtml: 'sp4-library-wishlist.html',
    route: '/library/wishlist',
    auth: 'user',
    // Glob HOST-AGNOSTICI — vedi nota in testa al file.
    mock: async page => {
      await page.route('**/api/v1/wishlist', route =>
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(WISHLIST_FIXTURE),
        })
      );
      await page.route('**/api/v1/wishlist/highlights', route =>
        route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
      );
      // La wishlist page usa useLibrary per la mappa gameId→title; i fixture
      // portano già gameName inline, quindi la library può essere vuota.
      await page.route('**/api/v1/library**', route =>
        route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
      );
    },
  },
];
