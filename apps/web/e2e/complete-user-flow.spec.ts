import { expect, test } from './fixtures';
import { AuthHelper, USER_FIXTURES } from './pages';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('Complete User Flow: Register → Add Game → Upload PDF → View Rulebook', () => {
  const MOCK_BGG_GAME = {
    bggId: 9209,
    name: 'Ticket to Ride',
    yearPublished: 2004,
    minPlayers: 2,
    maxPlayers: 5,
    playingTime: 60,
    minPlayTime: 30,
    maxPlayTime: 60,
    thumbnailUrl:
      'https://cf.geekdo-images.com/ZWJg0dCdrWHxVnc0eFXK8w__thumb/img/I6nNMRCJ3NsLT8Ou2L1YE6SLSx8=/fit-in/200x150/filters:strip_icc()/pic66668.jpg',
    imageUrl:
      'https://cf.geekdo-images.com/ZWJg0dCdrWHxVnc0eFXK8w__original/img/r6K2nIJl9N8Vbdl-TLbMXwKLLlE=/0x0/filters:format(jpeg)/pic66668.jpg',
    description: 'Ticket to Ride is a cross-country train adventure game.',
    publishers: ['Days of Wonder'],
    designers: ['Alan R. Moon'],
    categories: ['Trains'],
    mechanics: ['Set Collection', 'Route Building'],
  };

  test.beforeEach(async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Mock authenticated session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Mock BGG Search
    await page.route(`${API_BASE}/api/v1/bgg/search*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          results: [
            {
              bggId: MOCK_BGG_GAME.bggId,
              name: MOCK_BGG_GAME.name,
              yearPublished: MOCK_BGG_GAME.yearPublished,
              thumbnailUrl: MOCK_BGG_GAME.thumbnailUrl,
            },
          ],
        }),
      });
    });

    // Mock BGG Details
    await page.route(`${API_BASE}/api/v1/bgg/games/${MOCK_BGG_GAME.bggId}`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_BGG_GAME),
      });
    });

    // Mock Create Game POST
    await page.route(`${API_BASE}/api/v1/games`, async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'test-game-id-123',
            title: MOCK_BGG_GAME.name,
            createdAt: new Date().toISOString(),
          }),
        });
      } else {
        await route.continue();
      }
    });

    // Mock Games endpoints (both list and single game)
    let gamesAdded = false;
    await page.route(`${API_BASE}/api/v1/games**`, async route => {
      const url = route.request().url();

      if (route.request().method() === 'GET') {
        // Check if it's a single game request (has UUID in path)
        if (url.includes('/api/v1/games/test-game-id-123')) {
          // Return single game object
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              id: 'test-game-id-123',
              title: MOCK_BGG_GAME.name,
              publisher: MOCK_BGG_GAME.publishers[0],
              yearPublished: MOCK_BGG_GAME.yearPublished,
              minPlayers: MOCK_BGG_GAME.minPlayers,
              maxPlayers: MOCK_BGG_GAME.maxPlayers,
              minPlayTimeMinutes: MOCK_BGG_GAME.minPlayTime,
              maxPlayTimeMinutes: MOCK_BGG_GAME.maxPlayTime,
              bggId: MOCK_BGG_GAME.bggId,
              createdAt: new Date().toISOString(),
            }),
          });
        } else {
          // Return games list
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(
              gamesAdded
                ? [
                    {
                      id: 'test-game-id-123',
                      title: MOCK_BGG_GAME.name,
                      publisher: MOCK_BGG_GAME.publishers[0],
                      yearPublished: MOCK_BGG_GAME.yearPublished,
                      minPlayers: MOCK_BGG_GAME.minPlayers,
                      maxPlayers: MOCK_BGG_GAME.maxPlayers,
                      minPlayTimeMinutes: MOCK_BGG_GAME.minPlayTime,
                      maxPlayTimeMinutes: MOCK_BGG_GAME.maxPlayTime,
                      bggId: MOCK_BGG_GAME.bggId,
                      createdAt: new Date().toISOString(),
                    },
                  ]
                : []
            ),
          });
          gamesAdded = true;
        }
      } else {
        await route.continue();
      }
    });

    // Mock PDF Upload
    await page.route(`${API_BASE}/api/v1/ingest/pdf`, async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            documentId: 'test-pdf-id-456',
          }),
        });
      }
    });

    // Mock PDF Processing Progress
    await page.route(`${API_BASE}/api/v1/pdfs/test-pdf-id-456/progress`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          currentStep: 'Completed',
          percentComplete: 100,
          elapsedTime: 'PT5S',
          pagesProcessed: 10,
          totalPages: 10,
          startedAt: new Date().toISOString(),
          completedAt: new Date().toISOString(),
        }),
      });
    });

    // Mock Get Game PDFs
    let pdfUploaded = false;
    await page.route(`${API_BASE}/api/v1/games/test-game-id-123/pdfs`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(
          pdfUploaded
            ? [
                {
                  id: 'test-pdf-id-456',
                  gameId: 'test-game-id-123',
                  fileName: 'ticket-to-ride_rulebook.pdf',
                  filePath: '/pdfs/wingspan_rulebook.pdf',
                  fileSizeBytes: 1024000,
                  processingStatus: 'Completed',
                  uploadedAt: new Date().toISOString(),
                  processedAt: new Date().toISOString(),
                  pageCount: 10,
                  documentType: 'base',
                  isPublic: false,
                },
              ]
            : []
        ),
      });
      pdfUploaded = true;
    });

    // Mock Get Game Rules
    await page.route(`${API_BASE}/api/v1/games/test-game-id-123/rules`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });

    // Mock PDF Download
    await page.route(`${API_BASE}/api/v1/pdfs/test-pdf-id-456/download`, async route => {
      // Return a minimal valid PDF
      const pdfContent =
        '%PDF-1.4\n1 0 obj\n<<\n/Type /Catalog\n/Pages 2 0 R\n>>\nendobj\n2 0 obj\n<<\n/Type /Pages\n/Kids [3 0 R]\n/Count 1\n>>\nendobj\n3 0 obj\n<<\n/Type /Page\n/Parent 2 0 R\n/MediaBox [0 0 612 792]\n>>\nendobj\nxref\n0 4\n0000000000 65535 f\n0000000009 00000 n\n0000000058 00000 n\n0000000115 00000 n\ntrailer\n<<\n/Size 4\n/Root 1 0 R\n>>\nstartxref\n190\n%%EOF';
      await route.fulfill({
        status: 200,
        contentType: 'application/pdf',
        body: pdfContent,
      });
    });
  });

  test('should complete full user flow: add game from BGG, upload PDF, and view rulebook', async ({
    page,
  }) => {
    // Step 1: Navigate to Add Game page
    await page.goto('/games/add');
    await expect(page).toHaveURL(/\/games\/add/);

    // Step 2: Search for game on BGG
    await page.fill('input[placeholder*="Cerca su BoardGameGeek"]', 'Ticket to Ride');
    await page.click('button:has-text("Cerca")');

    // Wait for search results
    await page.waitForSelector('text=Ticket to Ride');
    await expect(page.locator('text=Ticket to Ride').first()).toBeVisible();

    // Step 3: Add game
    await page.click('button:has-text("Aggiungi")');

    // Wait for redirect and success message
    await expect(page).toHaveURL(/\/games$/);
    await expect(page.locator('text=Gioco aggiunto con successo!')).toBeVisible();

    // Step 4: Navigate to game detail page
    await page.click('text=Ticket to Ride');
    await expect(page).toHaveURL(/\/games\/test-game-id-123/);

    // Step 5: Go to Rules tab
    await page.click('button:has-text("Rules")');
    await expect(page.locator('text=Rulebook PDFs')).toBeVisible();

    // Step 6: Click Upload Rulebook button
    await page.click('button:has-text("Upload Rulebook")');
    await expect(page.locator('text=PDF File')).toBeVisible();

    // Step 7: Upload PDF file
    // Use the real Ticket to Ride PDF
    const pdfPath =
      'd:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/tests/rulebook/ticket-to-ride_rulebook.pdf';

    await page.setInputFiles('input[type="file"]', pdfPath);

    // Wait for validation
    await expect(page.locator('text=ticket-to-ride_rulebook.pdf')).toBeVisible();

    // Click Upload PDF button
    await page.click('button:has-text("Upload PDF")');

    // Wait for upload to complete
    await page.waitForSelector('text=View PDF', { timeout: 10000 });

    // Step 8: Verify PDF appears in list
    await expect(page.locator('text=ticket-to-ride_rulebook.pdf')).toBeVisible();
    await expect(page.locator('text=10 pages')).toBeVisible();

    // Step 9: Click View PDF
    await page.click('button:has-text("View PDF")');

    // Wait for PDF viewer modal to open
    await expect(page.locator('text=ticket-to-ride_rulebook.pdf').last()).toBeVisible();

    // Verify PDF viewer controls are present
    await expect(page.locator('button:has-text("Previous")')).toBeVisible();
    await expect(page.locator('button:has-text("Next")')).toBeVisible();

    console.log('✅ Complete user flow test passed successfully!');
  });
});
