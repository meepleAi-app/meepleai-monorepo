/**
 * Unit tests for `deriveWizardState` (SP6 Phase C.1.A Foundation).
 *
 * Coverage targets (~50 tests):
 *   - 14 cells × happy path each
 *   - Cell precedence:
 *       wizard-cancelled > cancel-modal > offline > step-specific > defaults
 *   - Step boundary behavior (step1/step2/step3 dispatch)
 *   - Threshold edge cases (light 0.30, detection 0.50, conf 0.50)
 *   - Discriminant exhaustive narrowing (TypeScript compile-only checks)
 */

import { describe, expect, it } from 'vitest';

import {
  deriveWizardState,
  LIGHT_LOW_THRESHOLD,
  LOW_CONFIDENCE_THRESHOLD,
  PAGE_DETECTION_THRESHOLD,
  type DeriveStateInput,
  type QueryLike,
  type WizardFSMCell,
} from '../fsm';
import type { BggSearchResult, CatalogGameRef } from '../schemas';

// ---------------------------------------------------------------------------
// Test factories
// ---------------------------------------------------------------------------

function ok<T>(data: T): QueryLike<T> {
  return { isPending: false, isError: false, error: null, data };
}

function pending<T>(): QueryLike<T> {
  return { isPending: true, isError: false, error: null, data: undefined };
}

const SAMPLE_GAME_ID = '00000000-0000-4000-8000-0000000c0001';
const SAMPLE_BATCH_ID = '00000000-0000-4000-8000-00000000b001';

const CATALOG_GAMES: readonly CatalogGameRef[] = [
  {
    id: SAMPLE_GAME_ID,
    title: 'Nanolith',
    publisher: 'Self-published',
    coverImageUrl: null,
    sharedByCount: 142,
    isIndexed: true,
  },
  {
    id: '00000000-0000-4000-8000-0000000c0002',
    title: 'Brass Birmingham',
    publisher: 'Roxley',
    coverImageUrl: null,
    sharedByCount: 87,
    isIndexed: true,
  },
];

const BGG_RESULTS: readonly BggSearchResult[] = [
  {
    bggId: 224517,
    title: 'Brass: Birmingham',
    publisher: 'Roxley',
    yearPublished: 2018,
  },
];

function happyInput(overrides: Partial<DeriveStateInput> = {}): DeriveStateInput {
  return {
    step: 1,
    gameId: null,
    batchId: null,
    gameSearchQuery: ok(CATALOG_GAMES),
    searchInput: '',
    activeTab: 'catalog',
    bggSearchQuery: ok([]),
    cameraPermission: 'prompt',
    lightMeterValue: 1.0,
    detectionScore: 1.0,
    isCapturing: false,
    capturedCount: 0,
    batchStatus: ok({
      status: 'Processing',
      totalPages: 0,
      processedPages: 0,
      averageConfidence: null,
    }),
    isOffline: false,
    retryAttempt: 0,
    nextRetryInMs: 0,
    cancelModalOpen: false,
    isWizardCancelled: false,
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// Step 1 — Game selection
// ---------------------------------------------------------------------------

describe('deriveWizardState — step1-default', () => {
  it('returns step1-default when no search input + catalog tab', () => {
    const state = deriveWizardState(happyInput());
    expect(state.kind).toBe('step1-default');
    if (state.kind === 'step1-default') {
      expect(state.catalogResults).toEqual(CATALOG_GAMES);
      expect(state.selectedGameId).toBe(null);
    }
  });

  it('exposes selectedGameId when ?gameId is set', () => {
    const state = deriveWizardState(happyInput({ gameId: SAMPLE_GAME_ID }));
    expect(state.kind).toBe('step1-default');
    if (state.kind === 'step1-default') {
      expect(state.selectedGameId).toBe(SAMPLE_GAME_ID);
    }
  });

  it('falls back to empty array when query data is undefined', () => {
    const state = deriveWizardState(
      happyInput({ gameSearchQuery: ok(undefined as unknown as readonly CatalogGameRef[]) })
    );
    expect(state.kind).toBe('step1-default');
    if (state.kind === 'step1-default') {
      expect(state.catalogResults).toEqual([]);
    }
  });

  it('whitespace-only search input is treated as empty', () => {
    const state = deriveWizardState(happyInput({ searchInput: '   ' }));
    expect(state.kind).toBe('step1-default');
  });
});

describe('deriveWizardState — step1-searching', () => {
  it('returns step1-searching when search input has matches', () => {
    const state = deriveWizardState(
      happyInput({
        searchInput: 'brass',
        gameSearchQuery: ok([CATALOG_GAMES[1]]),
      })
    );
    expect(state.kind).toBe('step1-searching');
    if (state.kind === 'step1-searching') {
      expect(state.query).toBe('brass');
      expect(state.activeTab).toBe('catalog');
      expect(state.catalogResults).toHaveLength(1);
    }
  });

  it('preserves bgg results when active tab is bgg + has data', () => {
    const state = deriveWizardState(
      happyInput({
        searchInput: 'brass',
        activeTab: 'bgg',
        gameSearchQuery: ok([]),
        bggSearchQuery: ok(BGG_RESULTS),
      })
    );
    expect(state.kind).toBe('step1-searching');
    if (state.kind === 'step1-searching') {
      expect(state.bggResults).toEqual(BGG_RESULTS);
    }
  });

  it('trims surrounding whitespace from query', () => {
    const state = deriveWizardState(happyInput({ searchInput: '  brass  ' }));
    if (state.kind === 'step1-searching') {
      expect(state.query).toBe('brass');
    }
  });
});

describe('deriveWizardState — step1-no-results', () => {
  it('returns step1-no-results when catalog tab + 0 catalog results', () => {
    const state = deriveWizardState(
      happyInput({
        searchInput: 'flibbertigibbet',
        gameSearchQuery: ok([]),
        bggSearchQuery: ok([]),
      })
    );
    expect(state.kind).toBe('step1-no-results');
    if (state.kind === 'step1-no-results') {
      expect(state.query).toBe('flibbertigibbet');
    }
  });

  it('does NOT return step1-no-results when bgg tab even with 0 catalog', () => {
    const state = deriveWizardState(
      happyInput({
        searchInput: 'flibbertigibbet',
        activeTab: 'bgg',
        gameSearchQuery: ok([]),
        bggSearchQuery: ok(BGG_RESULTS),
      })
    );
    expect(state.kind).toBe('step1-searching');
  });
});

describe('deriveWizardState — step1-bgg-loading', () => {
  it('returns step1-bgg-loading when bgg tab + bgg query pending', () => {
    const state = deriveWizardState(
      happyInput({
        searchInput: 'gloomhaven',
        activeTab: 'bgg',
        bggSearchQuery: pending(),
      })
    );
    expect(state.kind).toBe('step1-bgg-loading');
    if (state.kind === 'step1-bgg-loading') {
      expect(state.query).toBe('gloomhaven');
    }
  });

  it('does NOT loading-cell when active tab is catalog (even if bgg pending)', () => {
    const state = deriveWizardState(
      happyInput({
        searchInput: 'gloomhaven',
        activeTab: 'catalog',
        bggSearchQuery: pending(),
        gameSearchQuery: ok([CATALOG_GAMES[0]]),
      })
    );
    expect(state.kind).toBe('step1-searching');
  });
});

// ---------------------------------------------------------------------------
// Step 2 — Photo capture
// ---------------------------------------------------------------------------

describe('deriveWizardState — step2-ready', () => {
  it('returns step2-ready when permission granted + light + detection OK', () => {
    const state = deriveWizardState(
      happyInput({
        step: 2,
        gameId: SAMPLE_GAME_ID,
        cameraPermission: 'granted',
        lightMeterValue: 0.75,
        detectionScore: 0.95,
      })
    );
    expect(state.kind).toBe('step2-ready');
    if (state.kind === 'step2-ready') {
      expect(state.permissionState).toBe('granted');
      expect(state.gameId).toBe(SAMPLE_GAME_ID);
      expect(state.capturedCount).toBe(0);
    }
  });

  it('preserves capturedCount across re-derivations', () => {
    const state = deriveWizardState(
      happyInput({
        step: 2,
        gameId: SAMPLE_GAME_ID,
        cameraPermission: 'granted',
        capturedCount: 7,
      })
    );
    if (state.kind === 'step2-ready') {
      expect(state.capturedCount).toBe(7);
    }
  });

  it("treats permission='prompt' as ready (initial CTA)", () => {
    const state = deriveWizardState(
      happyInput({
        step: 2,
        gameId: SAMPLE_GAME_ID,
        cameraPermission: 'prompt',
      })
    );
    expect(state.kind).toBe('step2-ready');
    if (state.kind === 'step2-ready') {
      expect(state.permissionState).toBe('prompt');
    }
  });
});

describe('deriveWizardState — step2-capturing', () => {
  it('returns step2-capturing when isCapturing flag is true', () => {
    const state = deriveWizardState(
      happyInput({
        step: 2,
        gameId: SAMPLE_GAME_ID,
        cameraPermission: 'granted',
        isCapturing: true,
        capturedCount: 3,
      })
    );
    expect(state.kind).toBe('step2-capturing');
    if (state.kind === 'step2-capturing') {
      expect(state.capturedCount).toBe(3);
    }
  });

  it('takes precedence over low-light when both true', () => {
    const state = deriveWizardState(
      happyInput({
        step: 2,
        gameId: SAMPLE_GAME_ID,
        cameraPermission: 'granted',
        isCapturing: true,
        lightMeterValue: 0.1, // also low
      })
    );
    expect(state.kind).toBe('step2-capturing');
  });
});

describe('deriveWizardState — step2-low-light', () => {
  it('returns step2-low-light when light below threshold', () => {
    const state = deriveWizardState(
      happyInput({
        step: 2,
        gameId: SAMPLE_GAME_ID,
        cameraPermission: 'granted',
        lightMeterValue: 0.15,
      })
    );
    expect(state.kind).toBe('step2-low-light');
  });

  it('threshold edge — exactly LIGHT_LOW_THRESHOLD is NOT low (>=)', () => {
    const state = deriveWizardState(
      happyInput({
        step: 2,
        gameId: SAMPLE_GAME_ID,
        cameraPermission: 'granted',
        lightMeterValue: LIGHT_LOW_THRESHOLD,
      })
    );
    expect(state.kind).toBe('step2-ready');
  });

  it('just below threshold IS low', () => {
    const state = deriveWizardState(
      happyInput({
        step: 2,
        gameId: SAMPLE_GAME_ID,
        cameraPermission: 'granted',
        lightMeterValue: LIGHT_LOW_THRESHOLD - 0.001,
      })
    );
    expect(state.kind).toBe('step2-low-light');
  });
});

describe('deriveWizardState — step2-failed', () => {
  it('returns step2-failed when detection score below threshold', () => {
    const state = deriveWizardState(
      happyInput({
        step: 2,
        gameId: SAMPLE_GAME_ID,
        cameraPermission: 'granted',
        detectionScore: 0.2,
      })
    );
    expect(state.kind).toBe('step2-failed');
    if (state.kind === 'step2-failed') {
      expect(state.detectionScore).toBe(0.2);
    }
  });

  it('low-light overrides detection-failed when both true', () => {
    const state = deriveWizardState(
      happyInput({
        step: 2,
        gameId: SAMPLE_GAME_ID,
        cameraPermission: 'granted',
        lightMeterValue: 0.15,
        detectionScore: 0.2,
      })
    );
    expect(state.kind).toBe('step2-low-light');
  });

  it('threshold edge — exactly PAGE_DETECTION_THRESHOLD is NOT failed', () => {
    const state = deriveWizardState(
      happyInput({
        step: 2,
        gameId: SAMPLE_GAME_ID,
        cameraPermission: 'granted',
        detectionScore: PAGE_DETECTION_THRESHOLD,
      })
    );
    expect(state.kind).toBe('step2-ready');
  });
});

describe('deriveWizardState — step2-denied', () => {
  it('returns step2-denied when permission=denied', () => {
    const state = deriveWizardState(
      happyInput({
        step: 2,
        gameId: SAMPLE_GAME_ID,
        cameraPermission: 'denied',
      })
    );
    expect(state.kind).toBe('step2-denied');
    if (state.kind === 'step2-denied') {
      expect(state.permissionState).toBe('denied');
    }
  });

  it('returns step2-denied when permission=unsupported', () => {
    const state = deriveWizardState(
      happyInput({
        step: 2,
        gameId: SAMPLE_GAME_ID,
        cameraPermission: 'unsupported',
      })
    );
    expect(state.kind).toBe('step2-denied');
    if (state.kind === 'step2-denied') {
      expect(state.permissionState).toBe('unsupported');
    }
  });

  it('overrides low-light + detection-failed', () => {
    const state = deriveWizardState(
      happyInput({
        step: 2,
        gameId: SAMPLE_GAME_ID,
        cameraPermission: 'denied',
        lightMeterValue: 0.05,
        detectionScore: 0.1,
      })
    );
    expect(state.kind).toBe('step2-denied');
  });

  it('overrides isCapturing transient', () => {
    const state = deriveWizardState(
      happyInput({
        step: 2,
        gameId: SAMPLE_GAME_ID,
        cameraPermission: 'denied',
        isCapturing: true,
      })
    );
    expect(state.kind).toBe('step2-denied');
  });

  it('handles missing gameId by synthesizing empty string', () => {
    const state = deriveWizardState(
      happyInput({ step: 2, gameId: null, cameraPermission: 'denied' })
    );
    if (state.kind === 'step2-denied') {
      expect(state.gameId).toBe('');
    }
  });
});

// ---------------------------------------------------------------------------
// Step 3 — Indexing progress
// ---------------------------------------------------------------------------

describe('deriveWizardState — step3-progress', () => {
  it('returns step3-progress when status=Processing', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        batchId: SAMPLE_BATCH_ID,
        batchStatus: ok({
          status: 'Processing',
          totalPages: 12,
          processedPages: 7,
          averageConfidence: null,
        }),
      })
    );
    expect(state.kind).toBe('step3-progress');
    if (state.kind === 'step3-progress') {
      expect(state.batchId).toBe(SAMPLE_BATCH_ID);
      expect(state.processedPages).toBe(7);
      expect(state.totalPages).toBe(12);
    }
  });

  it('returns step3-progress when status=Pending', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        batchId: SAMPLE_BATCH_ID,
        batchStatus: ok({
          status: 'Pending',
          totalPages: 0,
          processedPages: 0,
          averageConfidence: null,
        }),
      })
    );
    expect(state.kind).toBe('step3-progress');
  });

  it('returns step3-progress with 0/0 when batch status missing', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        batchId: SAMPLE_BATCH_ID,
        batchStatus: pending(),
      })
    );
    expect(state.kind).toBe('step3-progress');
    if (state.kind === 'step3-progress') {
      expect(state.processedPages).toBe(0);
      expect(state.totalPages).toBe(0);
    }
  });
});

describe('deriveWizardState — step3-partial', () => {
  it('returns step3-partial when failedPageNumbers non-empty', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        batchId: SAMPLE_BATCH_ID,
        batchStatus: ok({
          status: 'Completed',
          totalPages: 12,
          processedPages: 12,
          averageConfidence: 0.65,
          failedPageNumbers: [3, 7],
        }),
      })
    );
    expect(state.kind).toBe('step3-partial');
    if (state.kind === 'step3-partial') {
      expect(state.lowConfidencePages).toEqual([3, 7]);
    }
  });

  it('returns step3-partial when avg conf below threshold', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        batchId: SAMPLE_BATCH_ID,
        batchStatus: ok({
          status: 'Completed',
          totalPages: 12,
          processedPages: 12,
          averageConfidence: 0.45,
          failedPageNumbers: [],
        }),
      })
    );
    expect(state.kind).toBe('step3-partial');
  });

  it('threshold edge — avg conf exactly LOW_CONFIDENCE_THRESHOLD is NOT partial', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        batchId: SAMPLE_BATCH_ID,
        batchStatus: ok({
          status: 'Completed',
          totalPages: 12,
          processedPages: 12,
          averageConfidence: LOW_CONFIDENCE_THRESHOLD,
          failedPageNumbers: [],
        }),
      })
    );
    expect(state.kind).toBe('step3-complete');
  });
});

describe('deriveWizardState — step3-complete', () => {
  it('returns step3-complete when all conditions OK', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        batchId: SAMPLE_BATCH_ID,
        batchStatus: ok({
          status: 'Completed',
          totalPages: 12,
          processedPages: 12,
          averageConfidence: 0.92,
          failedPageNumbers: [],
        }),
      })
    );
    expect(state.kind).toBe('step3-complete');
    if (state.kind === 'step3-complete') {
      expect(state.totalPages).toBe(12);
    }
  });

  it('treats null averageConfidence as completed (interim heuristic)', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        batchId: SAMPLE_BATCH_ID,
        batchStatus: ok({
          status: 'Completed',
          totalPages: 12,
          processedPages: 12,
          averageConfidence: null,
          failedPageNumbers: [],
        }),
      })
    );
    expect(state.kind).toBe('step3-complete');
  });
});

describe('deriveWizardState — step3-offline', () => {
  it('returns step3-offline when isOffline=true', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        batchId: SAMPLE_BATCH_ID,
        batchStatus: ok({
          status: 'Processing',
          totalPages: 12,
          processedPages: 5,
          averageConfidence: null,
        }),
        isOffline: true,
        retryAttempt: 2,
        nextRetryInMs: 4_000,
      })
    );
    expect(state.kind).toBe('step3-offline');
    if (state.kind === 'step3-offline') {
      expect(state.retryAttempt).toBe(2);
      expect(state.nextRetryInMs).toBe(4_000);
    }
  });

  it('overrides Completed-with-low-conf (offline takes precedence)', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        batchId: SAMPLE_BATCH_ID,
        batchStatus: ok({
          status: 'Completed',
          totalPages: 12,
          processedPages: 12,
          averageConfidence: 0.4,
          failedPageNumbers: [3, 7],
        }),
        isOffline: true,
      })
    );
    expect(state.kind).toBe('step3-offline');
  });

  it('overrides Completed (offline takes precedence)', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        batchId: SAMPLE_BATCH_ID,
        batchStatus: ok({
          status: 'Completed',
          totalPages: 12,
          processedPages: 12,
          averageConfidence: 0.92,
          failedPageNumbers: [],
        }),
        isOffline: true,
      })
    );
    expect(state.kind).toBe('step3-offline');
  });
});

// ---------------------------------------------------------------------------
// Cell precedence — modal & terminal
// ---------------------------------------------------------------------------

describe('deriveWizardState — cancel-modal', () => {
  it('returns step3-cancel-modal when cancelModalOpen=true on step 3', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        batchId: SAMPLE_BATCH_ID,
        cancelModalOpen: true,
        batchStatus: ok({
          status: 'Processing',
          totalPages: 12,
          processedPages: 5,
          averageConfidence: null,
        }),
      })
    );
    expect(state.kind).toBe('step3-cancel-modal');
  });

  it('exposes previousCell for backdrop rendering', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        batchId: SAMPLE_BATCH_ID,
        cancelModalOpen: true,
        batchStatus: ok({
          status: 'Processing',
          totalPages: 12,
          processedPages: 5,
          averageConfidence: null,
        }),
      })
    );
    if (state.kind === 'step3-cancel-modal') {
      expect(state.previousCell.kind).toBe('step3-progress');
      // previousCell must NOT recurse infinitely
      expect(state.previousCell).not.toHaveProperty('previousCell');
    }
  });

  it('overrides offline state', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        batchId: SAMPLE_BATCH_ID,
        cancelModalOpen: true,
        isOffline: true,
        batchStatus: ok({
          status: 'Processing',
          totalPages: 12,
          processedPages: 5,
          averageConfidence: null,
        }),
      })
    );
    expect(state.kind).toBe('step3-cancel-modal');
  });

  it('does NOT activate when batchId is null (no active batch to cancel)', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        batchId: null,
        cancelModalOpen: true,
      })
    );
    expect(state.kind).not.toBe('step3-cancel-modal');
  });
});

describe('deriveWizardState — wizard-cancelled (terminal)', () => {
  it('returns wizard-cancelled when isWizardCancelled=true', () => {
    const state = deriveWizardState(happyInput({ isWizardCancelled: true }));
    expect(state.kind).toBe('wizard-cancelled');
    if (state.kind === 'wizard-cancelled') {
      expect(state.reason).toBe('user-action');
    }
  });

  it('overrides cancel-modal (terminal beats modal)', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        batchId: SAMPLE_BATCH_ID,
        cancelModalOpen: true,
        isWizardCancelled: true,
      })
    );
    expect(state.kind).toBe('wizard-cancelled');
  });

  it('overrides offline state', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        isOffline: true,
        isWizardCancelled: true,
      })
    );
    expect(state.kind).toBe('wizard-cancelled');
  });
});

// ---------------------------------------------------------------------------
// Step boundary dispatch
// ---------------------------------------------------------------------------

describe('deriveWizardState — step boundary dispatch', () => {
  it('step=1 always dispatches to step1-* cell', () => {
    const state = deriveWizardState(happyInput({ step: 1 }));
    expect(state.kind.startsWith('step1-')).toBe(true);
  });

  it('step=2 always dispatches to step2-* cell', () => {
    const state = deriveWizardState(
      happyInput({
        step: 2,
        gameId: SAMPLE_GAME_ID,
        cameraPermission: 'granted',
      })
    );
    expect(state.kind.startsWith('step2-')).toBe(true);
  });

  it('step=3 always dispatches to step3-* cell', () => {
    const state = deriveWizardState(
      happyInput({
        step: 3,
        batchId: SAMPLE_BATCH_ID,
      })
    );
    expect(state.kind.startsWith('step3-')).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Discriminant exhaustive narrowing (compile-time check via runtime assertion)
// ---------------------------------------------------------------------------

describe('deriveWizardState — exhaustive narrowing', () => {
  it('every cell kind is reachable via deriveWizardState', () => {
    const seen = new Set<WizardFSMCell['kind']>();

    const cells: WizardFSMCell[] = [
      // step1-*
      deriveWizardState(happyInput()),
      deriveWizardState(happyInput({ searchInput: 'brass' })),
      deriveWizardState(happyInput({ searchInput: 'xyz', gameSearchQuery: ok([]) })),
      deriveWizardState(
        happyInput({
          searchInput: 'gloomhaven',
          activeTab: 'bgg',
          bggSearchQuery: pending(),
        })
      ),
      // step2-*
      deriveWizardState(
        happyInput({
          step: 2,
          gameId: SAMPLE_GAME_ID,
          cameraPermission: 'granted',
        })
      ),
      deriveWizardState(
        happyInput({
          step: 2,
          gameId: SAMPLE_GAME_ID,
          cameraPermission: 'granted',
          isCapturing: true,
        })
      ),
      deriveWizardState(
        happyInput({
          step: 2,
          gameId: SAMPLE_GAME_ID,
          cameraPermission: 'granted',
          lightMeterValue: 0.1,
        })
      ),
      deriveWizardState(
        happyInput({
          step: 2,
          gameId: SAMPLE_GAME_ID,
          cameraPermission: 'granted',
          detectionScore: 0.2,
        })
      ),
      deriveWizardState(
        happyInput({
          step: 2,
          gameId: SAMPLE_GAME_ID,
          cameraPermission: 'denied',
        })
      ),
      // step3-*
      deriveWizardState(happyInput({ step: 3, batchId: SAMPLE_BATCH_ID })),
      deriveWizardState(
        happyInput({
          step: 3,
          batchId: SAMPLE_BATCH_ID,
          batchStatus: ok({
            status: 'Completed',
            totalPages: 12,
            processedPages: 12,
            averageConfidence: 0.4,
            failedPageNumbers: [3],
          }),
        })
      ),
      deriveWizardState(
        happyInput({
          step: 3,
          batchId: SAMPLE_BATCH_ID,
          batchStatus: ok({
            status: 'Completed',
            totalPages: 12,
            processedPages: 12,
            averageConfidence: 0.92,
            failedPageNumbers: [],
          }),
        })
      ),
      deriveWizardState(
        happyInput({
          step: 3,
          batchId: SAMPLE_BATCH_ID,
          isOffline: true,
        })
      ),
      deriveWizardState(
        happyInput({
          step: 3,
          batchId: SAMPLE_BATCH_ID,
          cancelModalOpen: true,
        })
      ),
      // terminal
      deriveWizardState(happyInput({ isWizardCancelled: true })),
    ];

    for (const cell of cells) seen.add(cell.kind);

    expect(seen).toEqual(
      new Set<WizardFSMCell['kind']>([
        'step1-default',
        'step1-searching',
        'step1-no-results',
        'step1-bgg-loading',
        'step2-ready',
        'step2-capturing',
        'step2-low-light',
        'step2-failed',
        'step2-denied',
        'step3-progress',
        'step3-partial',
        'step3-complete',
        'step3-offline',
        'step3-cancel-modal',
        'wizard-cancelled',
      ])
    );
  });
});
