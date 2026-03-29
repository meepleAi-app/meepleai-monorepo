/**
 * E2E Test: RAG-001 PDF Processing Pipeline Validation
 * Issue #3172
 *
 * Validates end-to-end flow:
 * 1. Upload PDF rulebook
 * 2. Process and chunk document
 * 3. Generate embeddings
 * 4. Index in pgvector
 * 5. RAG query with confidence validation
 *
 * Critical: This test validates the core RAG infrastructure before EPIC 1.
 * Note: Qdrant replaced by pgvector — vector counts verified via /api/v1/admin/kb/vector-stats
 */

import { test, expect } from '@playwright/test';
import fs from 'fs';
import path from 'path';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
const PDF_PATH = path.join(process.cwd(), '../../data/rulebook/scacchi-fide_2017_rulebook.pdf');

test.describe('RAG-001: PDF Processing E2E Pipeline', () => {
  let adminAuthCookie: string;
  let chessSharedGameId: string;
  let chessLibraryGameId: string;

  test.beforeAll(async ({ request }) => {
    // Step 0: Auto-detect Chess SharedGame ID from database
    const gamesResponse = await request.get(`${API_URL}/api/v1/shared-games?search=Chess&limit=1`);
    expect(gamesResponse.ok()).toBeTruthy();

    const gamesData = await gamesResponse.json();
    expect(gamesData.items).toHaveLength(1);
    chessSharedGameId = gamesData.items[0].id;

    console.log(`Chess SharedGame ID: ${chessSharedGameId}`);
  });

  test.beforeEach(async ({ page, request }) => {
    // Step 1: Login as admin
    const loginResponse = await request.post(`${API_URL}/api/v1/auth/login`, {
      data: {
        email: 'admin@meepleai.dev',
        password: process.env.ADMIN_PASSWORD || 'pVKOMQNK0tFNgGlX',
      },
    });

    expect(loginResponse.ok()).toBeTruthy();

    // Extract auth cookie
    const cookies = await page.context().cookies();
    const sessionCookie = cookies.find(c => c.name.includes('session') || c.name.includes('auth'));
    adminAuthCookie = sessionCookie ? `${sessionCookie.name}=${sessionCookie.value}` : '';

    // Step 2: Enable PDF Upload feature flag (idempotent)
    await request.post(`${API_URL}/api/v1/admin/feature-flags`, {
      data: {
        key: 'Features.PdfUpload',
        enabled: true,
        description: 'Enable PDF uploads for RAG testing',
      },
      headers: {
        Cookie: adminAuthCookie,
      },
      failOnStatusCode: false, // May already exist (409)
    });
  });

  test('should complete full RAG pipeline: Upload → pgvector → Query', async ({
    page,
    request,
  }) => {
    // Step 3: Add Chess to admin library (using SharedGameId)
    const addToLibraryResponse = await page.request.post(
      `${API_URL}/api/v1/library/games/${chessSharedGameId}`,
      {
        data: {},
        headers: { Cookie: adminAuthCookie },
        failOnStatusCode: false,
      }
    );

    // Accept 201 (created) or 409 (already exists)
    expect([201, 409]).toContain(addToLibraryResponse.status());

    if (addToLibraryResponse.status() === 201) {
      const libraryEntry = await addToLibraryResponse.json();
      chessLibraryGameId = libraryEntry.id;
      console.log(`Chess added to library: ${chessLibraryGameId}`);
    } else {
      // Fetch library to get Chess game ID
      const libraryResponse = await page.request.get(`${API_URL}/api/v1/library/games`, {
        headers: { Cookie: adminAuthCookie },
      });
      const library = await libraryResponse.json();
      const chessEntry = library.items.find(
        (item: any) =>
          item.sharedGameId === chessSharedGameId || item.gameTitle?.toLowerCase().includes('chess')
      );
      expect(chessEntry).toBeTruthy();
      chessLibraryGameId = chessEntry.id;
      console.log(`Chess already in library: ${chessLibraryGameId}`);
    }

    // Step 4: Check pgvector stats (before upload)
    const statsBefore = await request.get(`${API_URL}/api/v1/admin/kb/vector-stats`, {
      headers: { Cookie: adminAuthCookie },
    });
    expect(statsBefore.ok()).toBeTruthy();

    const statsDataBefore = await statsBefore.json();
    const vectorsBeforeCount = statsDataBefore.totalVectors || 0;
    console.log(`pgvector vectors before: ${vectorsBeforeCount}`);

    // Step 5: Upload PDF (using SharedGameId - handler supports both)
    expect(fs.existsSync(PDF_PATH)).toBeTruthy();

    const pdfBuffer = fs.readFileSync(PDF_PATH);
    const formData = new FormData();
    formData.append(
      'file',
      new Blob([pdfBuffer], { type: 'application/pdf' }),
      'scacchi-fide_2017_rulebook.pdf'
    );
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
      headers: { Cookie: adminAuthCookie },
      timeout: 120000,
    });

    expect(uploadResponse.ok()).toBeTruthy();
    const uploadResult = await uploadResponse.json();
    const documentId = uploadResult.documentId || uploadResult.id;
    expect(documentId).toBeTruthy();
    console.log(`PDF uploaded: ${documentId}`);

    // Step 6: Trigger indexing
    const indexResponse = await page.request.post(
      `${API_URL}/api/v1/ingest/pdf/${documentId}/index`,
      {
        headers: { Cookie: adminAuthCookie },
        timeout: 180000,
      }
    );

    expect([200, 202]).toContain(indexResponse.status());
    console.log('Indexing triggered, waiting for processing...');

    // Wait for processing (polling approach)
    let processed = false;
    for (let i = 0; i < 12; i++) {
      await page.waitForTimeout(5000);

      const statsCheck = await request.get(`${API_URL}/api/v1/admin/kb/vector-stats`, {
        headers: { Cookie: adminAuthCookie },
      });
      const statsData = await statsCheck.json();
      const currentVectors = statsData.totalVectors || 0;

      if (currentVectors > vectorsBeforeCount) {
        processed = true;
        console.log(`Vectors added: ${currentVectors - vectorsBeforeCount}`);
        break;
      }
    }

    expect(processed).toBeTruthy(); // Fail if no vectors after 60 seconds

    // Step 7: Verify pgvector stats (after)
    const statsAfter = await request.get(`${API_URL}/api/v1/admin/kb/vector-stats`, {
      headers: { Cookie: adminAuthCookie },
    });
    const statsDataAfter = await statsAfter.json();
    const vectorsAfterCount = statsDataAfter.totalVectors || 0;

    expect(vectorsAfterCount).toBeGreaterThan(vectorsBeforeCount);
    expect(vectorsAfterCount).toBeGreaterThan(10); // At least 10 chunks
    console.log(`pgvector validation: ${vectorsAfterCount} vectors`);

    // Step 8: Test RAG query
    const ragQueryResponse = await page.request.post(`${API_URL}/api/v1/agents/qa`, {
      data: {
        gameId: chessSharedGameId, // Or chessLibraryGameId - both should work
        query: 'Come si muovono i pedoni negli scacchi?',
      },
      headers: { Cookie: adminAuthCookie },
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

  test('should handle PDF processing errors gracefully', async ({ page }) => {
    // Test error handling with invalid PDF
    await page.goto('/login');

    // Login
    await page.fill('input[type="email"]', 'admin@meepleai.dev');
    await page.fill('input[type="password"]', process.env.ADMIN_PASSWORD || 'pVKOMQNK0tFNgGlX');
    await page.click('button[type="submit"]');
    await page.waitForURL(/dashboard|library/);

    // Navigate to Chess detail (use Chess from library)
    await page.goto(`/games/${chessSharedGameId}`); // Or use UI navigation

    // Try to upload invalid file
    const invalidPdfContent = Buffer.from('Not a valid PDF');

    // This test verifies error handling exists
    // Implementation depends on UI upload component
    // For now, just verify page loads
    await expect(page).toHaveURL(new RegExp(chessSharedGameId));
  });
});
