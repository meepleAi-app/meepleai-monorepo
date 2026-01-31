/**
 * E2E Test: RAG-001 PDF Processing Pipeline Validation
 * Issue #3172
 *
 * Validates end-to-end flow:
 * 1. Upload PDF rulebook
 * 2. Process and chunk document
 * 3. Generate embeddings
 * 4. Index in Qdrant
 * 5. RAG query with confidence validation
 *
 * Critical: This test validates the core RAG infrastructure before EPIC 1.
 */

import fs from 'fs';
import path from 'path';

import { test, expect } from '@playwright/test';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
const QDRANT_URL = 'http://localhost:6333';
const PDF_PATH = path.join(process.cwd(), '../../data/rulebook/scacchi-fide_2017_rulebook.pdf');

test.describe('RAG-001: PDF Processing E2E Pipeline', () => {
  // Use fixed Chess SharedGameId (seeded by AutoConfigurationService)
  // If ID changes, test will fail with clear error message
  const CHESS_SHARED_GAME_ID_PLACEHOLDER = '00000000-0000-0000-0000-000000000000';

  let chessSharedGameId: string = CHESS_SHARED_GAME_ID_PLACEHOLDER;

  test.beforeEach(async ({ page }) => {
    // Step 0: Auto-detect Chess ID if not set
    if (chessSharedGameId === CHESS_SHARED_GAME_ID_PLACEHOLDER) {
      const gamesResponse = await page.request.get(`${API_URL}/api/v1/shared-games?search=Chess&limit=1`);
      if (gamesResponse.ok()) {
        const gamesData = await gamesResponse.json();
        if (gamesData.items && gamesData.items.length > 0) {
          chessSharedGameId = gamesData.items[0].id;
          console.log(`✅ Chess SharedGame ID: ${chessSharedGameId}`);
        }
      }
    }

    // Step 1: Login as admin via UI (more reliable than API for cookie management)
    await page.goto('/login');
    await page.fill('input[name="email"]', 'admin@meepleai.dev');
    await page.fill('input[name="password"]', process.env.ADMIN_PASSWORD || 'pVKOMQNK0tFNgGlX');

    const loginButton = page.locator('button[type="submit"]');
    await loginButton.click();

    // Wait for redirect (admin goes to /admin, regular user to /dashboard)
    await page.waitForURL(/admin|dashboard|library/, { timeout: 10000 });
    console.log(`✅ Logged in, redirected to: ${page.url()}`);

    // Step 2: Enable PDF Upload feature flag via API
    await page.request.post(`${API_URL}/api/v1/admin/feature-flags`, {
      data: {
        key: 'Features.PdfUpload',
        enabled: true,
        description: 'Enable PDF uploads for RAG testing',
      },
      failOnStatusCode: false, // May already exist (409)
    });
  });

  test('should complete full RAG pipeline: Upload → Qdrant → Query', async ({ page }) => {
    test.setTimeout(180000); // 3 minutes for full pipeline
    // Step 3: Check Qdrant collection (before upload)
    const qdrantBefore = await page.request.get(`${QDRANT_URL}/collections/meepleai_documents`);
    expect(qdrantBefore.ok()).toBeTruthy();

    const qdrantDataBefore = await qdrantBefore.json();
    const vectorsBeforeCount = qdrantDataBefore.result.points_count || 0;
    console.log(`Qdrant vectors before: ${vectorsBeforeCount}`);

    // Step 5: Upload PDF (using SharedGameId - handler supports both)
    expect(fs.existsSync(PDF_PATH)).toBeTruthy();

    const pdfBuffer = fs.readFileSync(PDF_PATH);
    const formData = new FormData();
    formData.append('file', new Blob([pdfBuffer], { type: 'application/pdf' }), 'scacchi-fide_2017_rulebook.pdf');
    formData.append('gameId', chessSharedGameId); // Use SharedGameId (fix enables this!)
    formData.append('language', 'it');

    const uploadResponse = await page.request.post(`${API_URL}/api/v1/ingest/pdf`, {
      multipart: {
        file: {
          name: 'scacchi-fide_2017_rulebook.pdf',
          mimeType: 'application/pdf',
          buffer: pdfBuffer,
        },
        gameId: chessSharedGameId,
        language: 'it',
      },
      timeout: 120000,
    });

    console.log(`Upload response status: ${uploadResponse.status()}`);

    if (!uploadResponse.ok()) {
      const errorBody = await uploadResponse.text();
      console.log(`Upload error: ${errorBody}`);
    }

    expect(uploadResponse.ok()).toBeTruthy();
    const uploadResult = await uploadResponse.json();
    const documentId = uploadResult.documentId || uploadResult.id;
    expect(documentId).toBeTruthy();
    console.log(`PDF uploaded: ${documentId}`);

    // Step 6: Extract text from PDF
    const extractResponse = await page.request.post(
      `${API_URL}/api/v1/ingest/pdf/${documentId}/extract`,
      {
        timeout: 180000,
      }
    );

    console.log(`Text extraction status: ${extractResponse.status()}`);
    if (!extractResponse.ok()) {
      const extractError = await extractResponse.text();
      console.log(`Extraction error: ${extractError}`);
    }

    expect(extractResponse.ok()).toBeTruthy();
    console.log('Text extraction triggered, waiting 15s...');
    await page.waitForTimeout(15000); // Wait for extraction

    // Step 7: Trigger indexing
    const indexResponse = await page.request.post(
      `${API_URL}/api/v1/ingest/pdf/${documentId}/index`,
      {
        timeout: 180000,
      }
    );

    console.log(`Indexing response status: ${indexResponse.status()}`);
    if (!indexResponse.ok()) {
      const indexError = await indexResponse.text();
      console.log(`Indexing error: ${indexError}`);
    }

    expect([200, 202]).toContain(indexResponse.status());
    console.log('Indexing triggered, waiting for processing...');

    // Wait for processing (polling approach)
    let processed = false;
    for (let i = 0; i < 12; i++) {
      await page.waitForTimeout(5000);

      const qdrantCheck = await page.request.get(`${QDRANT_URL}/collections/meepleai_documents`);
      const qdrantData = await qdrantCheck.json();
      const currentVectors = qdrantData.result.points_count || 0;

      if (currentVectors > vectorsBeforeCount) {
        processed = true;
        console.log(`Vectors added: ${currentVectors - vectorsBeforeCount}`);
        break;
      }
    }

    expect(processed).toBeTruthy(); // Fail if no vectors after 60 seconds

    // Step 7: Verify Qdrant collection (after)
    const qdrantAfter = await page.request.get(`${QDRANT_URL}/collections/meepleai_documents`);
    const qdrantDataAfter = await qdrantAfter.json();
    const vectorsAfterCount = qdrantDataAfter.result.points_count || 0;

    expect(vectorsAfterCount).toBeGreaterThan(vectorsBeforeCount);
    expect(vectorsAfterCount).toBeGreaterThan(10); // At least 10 chunks
    console.log(`Qdrant validation: ${vectorsAfterCount} vectors`);

    // Step 8: Test RAG query
    const ragQueryResponse = await page.request.post(`${API_URL}/api/v1/agents/qa`, {
      data: {
        gameId: chessSharedGameId,
        query: 'Come si muovono i pedoni negli scacchi?',
      },
      timeout: 30000,
    });

    expect(ragQueryResponse.ok()).toBeTruthy();
    const ragResult = await ragQueryResponse.json();

    // Step 9: Validate RAG response
    expect(ragResult.answer).toBeTruthy();
    expect(ragResult.answer.length).toBeGreaterThan(50); // Meaningful answer
    expect(ragResult.confidence).toBeGreaterThanOrEqual(0.7); // Confidence threshold
    expect(ragResult.citations).toBeDefined();
    expect(ragResult.citations.length).toBeGreaterThan(0); // Has citations

    console.log(`RAG Query Success:`);
    console.log(`  Answer: ${ragResult.answer.substring(0, 100)}...`);
    console.log(`  Confidence: ${ragResult.confidence}`);
    console.log(`  Citations: ${ragResult.citations.length}`);

    // SUCCESS: All validation criteria met ✅
  });

  // Future: Add error handling test when UI upload component is implemented
  // test.skip('should handle PDF processing errors gracefully', ...);
});
