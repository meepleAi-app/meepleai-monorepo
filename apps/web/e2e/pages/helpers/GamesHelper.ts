/**
 * GamesHelper - Centralized games/PDF utilities
 *
 * Handles all games and PDF-related mocking operations including:
 * - Games catalog and search
 * - PDF upload and processing
 * - RuleSpec management
 * - Document ingestion
 *
 * Replaces legacy mockGames* and mockPDF* functions.
 */

import { Page } from '@playwright/test';

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

export interface Game {
  id: string;
  name: string;
  createdAt: string;
  publisher?: string;
  year?: number;
}

export class GamesHelper {
  constructor(private readonly page: Page) {}

  /**
   * Mock games list endpoint
   */
  async mockGamesList(games?: Game[]): Promise<void> {
    const defaultGames = games || [
      {
        id: 'game-1',
        name: 'Terraforming Mars',
        createdAt: new Date().toISOString(),
        publisher: 'FryxGames',
        year: 2016,
      },
      {
        id: 'game-2',
        name: 'Wingspan',
        createdAt: new Date().toISOString(),
        publisher: 'Stonemaier Games',
        year: 2019,
      },
    ];

    await this.page.route(`${apiBase}/api/v1/games*`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            games: defaultGames,
            totalCount: defaultGames.length,
            pageNumber: 1,
            pageSize: 10,
            totalPages: 1,
          }),
        });
      } else if (route.request().method() === 'POST') {
        const newGame = route.request().postDataJSON() as Partial<Game>;
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'game-new',
            ...newGame,
            createdAt: new Date().toISOString(),
          }),
        });
      }
    });
  }

  /**
   * Mock PDF upload/ingest endpoint
   */
  async mockPDFUpload(documentId: string = 'doc-123'): Promise<void> {
    await this.page.route(`${apiBase}/ingest/pdf`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ documentId }),
      });
    });
  }

  /**
   * Mock PDF list for a game
   */
  async mockGamePDFs(gameId: string, pdfs?: Array<any>): Promise<void> {
    const defaultPdfs = pdfs || [];

    await this.page.route(`${apiBase}/games/${gameId}/pdfs`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ pdfs: defaultPdfs }),
      });
    });
  }

  /**
   * Mock RuleSpec endpoints (GET/PUT)
   */
  async mockRuleSpec(gameId: string, ruleSpec?: any): Promise<void> {
    const defaultRuleSpec = ruleSpec || {
      setup: 'Default setup instructions',
      gameplay: 'Default gameplay rules',
      winning: 'Default winning conditions',
    };

    await this.page.route(`${apiBase}/games/${gameId}/rulespec`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(defaultRuleSpec),
        });
      } else if (route.request().method() === 'PUT') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: route.request().postData() || JSON.stringify({}),
        });
      }
    });
  }

  /**
   * Mock PDF processing/parsing endpoint
   */
  async mockPDFProcessing(success: boolean = true): Promise<void> {
    await this.page.route(`${apiBase}/api/v1/pdf/process*`, async route => {
      if (success) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            success: true,
            extractedText: 'Sample game rules text',
            pages: 10,
          }),
        });
      } else {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Processing failed' }),
        });
      }
    });
  }

  /**
   * Mock PDF preview endpoint
   */
  async mockPDFPreview(pdfUrl?: string): Promise<void> {
    await this.page.route(`${apiBase}/api/v1/pdf/preview*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          url: pdfUrl || 'http://localhost:8080/pdfs/sample.pdf',
          thumbnails: [
            { page: 1, url: '/thumbnails/page-1.png' },
            { page: 2, url: '/thumbnails/page-2.png' },
          ],
        }),
      });
    });
  }

  /**
   * Mock game search endpoint
   */
  async mockGameSearch(query: string, results?: Game[]): Promise<void> {
    const defaultResults = results || [
      {
        id: 'game-search-1',
        name: 'Catan',
        createdAt: new Date().toISOString(),
      },
    ];

    await this.page.route(`${apiBase}/api/v1/games/search*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          results: defaultResults,
          query,
          totalResults: defaultResults.length,
        }),
      });
    });
  }

  /**
   * Mock game deletion endpoint
   */
  async mockGameDelete(success: boolean = true): Promise<void> {
    await this.page.route(`${apiBase}/api/v1/games/*`, async route => {
      if (route.request().method() === 'DELETE') {
        if (success) {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ success: true }),
          });
        } else {
          await route.fulfill({
            status: 400,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Delete failed' }),
          });
        }
      }
    });
  }

  /**
   * Mock PDF progress tracking endpoint
   */
  async mockPDFProgress(progress: number = 100): Promise<void> {
    await this.page.route(`${apiBase}/api/v1/pdf/progress*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          progress,
          status: progress === 100 ? 'completed' : 'processing',
        }),
      });
    });
  }

  /**
   * Mock complete PDF upload journey with stateful logic
   * Handles games list + PDF upload + PDF list updates
   *
   * @param initialGames - Starting games (defaults to Catan)
   * @returns Object with games and pdfs arrays for test assertions
   */
  async mockPdfUploadJourney(initialGames?: Array<any>): Promise<{
    games: Array<any>;
    pdfs: Array<any>;
  }> {
    const games = initialGames || [
      {
        id: 'game-1',
        name: 'Catan',
        description: 'A strategic board game about resource management',
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      },
    ];

    const pdfs: Array<any> = [];
    let nextPdfId = 1;
    const gameId = games[0].id;

    // Mock GET /games endpoint
    await this.page.route(`${apiBase}/games`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(games),
        });
      }
    });

    // Mock GET /games/{gameId}/pdfs endpoint (stateful - returns current pdfs array)
    await this.page.route(`${apiBase}/games/${gameId}/pdfs`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ pdfs: pdfs }),
        });
      }
    });

    // Mock POST /ingest/pdf endpoint (adds to pdfs array)
    await this.page.route(`${apiBase}/ingest/pdf`, async route => {
      if (route.request().method() === 'POST') {
        const documentId = `doc-${nextPdfId}`;

        const newPdf = {
          id: `pdf-${nextPdfId++}`,
          gameId: gameId,
          fileName: 'test-rulebook.pdf',
          fileSizeBytes: 1024 * 100, // 100 KB
          uploadedAt: new Date().toISOString(),
          status: 'Completed',
          pageCount: 10,
        };
        pdfs.push(newPdf);

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ documentId: documentId }),
        });
      }
    });

    return { games, pdfs };
  }

  /**
   * Mock PDF processing progress tracking with auto-advancing steps
   * Handles upload → progress polling → cancellation
   *
   * @param gameId - Target game ID
   * @returns Object with documentId and progress control
   */
  async mockPdfProcessingProgress(gameId: string): Promise<{
    documentId: string;
    getStep: () => string;
    cancel: () => void;
  }> {
    let currentStep = 'Uploading';
    let percentComplete = 10;
    let documentId = '';
    let cancelled = false;

    // Mock PDF upload endpoint
    await this.page.route(`${apiBase}/ingest/pdf`, async route => {
      if (route.request().method() === 'POST') {
        documentId = 'test-doc-123';
        currentStep = 'Extracting';
        percentComplete = 20;

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ documentId }),
        });
      }
    });

    // Mock PDF processing progress endpoint with auto-advancement
    await this.page.route(`${apiBase}/pdfs/*/progress`, async route => {
      if (route.request().method() === 'GET') {
        // Progress step definitions
        const progressSteps: Record<string, { next: string; percent: number; time: number }> = {
          Uploading: { next: 'Extracting', percent: 20, time: 120 },
          Extracting: { next: 'Chunking', percent: 40, time: 90 },
          Chunking: { next: 'Embedding', percent: 60, time: 60 },
          Embedding: { next: 'Indexing', percent: 80, time: 30 },
          Indexing: { next: 'Completed', percent: 100, time: 0 },
        };

        if (cancelled) {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              currentStep: 'Failed',
              percentComplete: percentComplete,
              errorMessage: 'Cancelled by user',
              updatedAt: new Date().toISOString(),
            }),
          });
          return;
        }

        const stepInfo = progressSteps[currentStep];
        if (stepInfo) {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              currentStep,
              percentComplete,
              estimatedTimeRemaining: stepInfo.time,
              updatedAt: new Date().toISOString(),
            }),
          });

          // Auto-advance to next step for next poll
          currentStep = stepInfo.next;
          percentComplete = stepInfo.percent;
        } else {
          // Completed state
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              currentStep: 'Completed',
              percentComplete: 100,
              updatedAt: new Date().toISOString(),
            }),
          });
        }
      }
    });

    // Mock PDF processing cancellation endpoint
    await this.page.route(`${apiBase}/pdfs/*/processing`, async route => {
      if (route.request().method() === 'DELETE') {
        cancelled = true;
        await route.fulfill({
          status: 204,
          contentType: 'application/json',
        });
      }
    });

    // Mock PDF list endpoint
    await this.page.route(`${apiBase}/games/${gameId}/pdfs`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ pdfs: [] }),
        });
      }
    });

    // Mock PDF text endpoint (for old progress polling)
    await this.page.route(`${apiBase}/pdfs/*/text`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: documentId,
            fileName: 'test.pdf',
            processingStatus: 'processing',
          }),
        });
      }
    });

    return {
      documentId,
      getStep: () => currentStep,
      cancel: () => {
        cancelled = true;
      },
    };
  }
}
