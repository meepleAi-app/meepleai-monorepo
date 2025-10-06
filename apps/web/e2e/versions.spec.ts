import { test, expect, Page } from '@playwright/test';

const apiBase = 'http://localhost:8080';

async function mockAuth(page: Page, shouldAuthenticate = true) {
  await page.route(`${apiBase}/auth/me`, async (route) => {
    if (!shouldAuthenticate) {
      await route.fulfill({ status: 401, contentType: 'application/json', body: JSON.stringify({ error: 'Unauthorized' }) });
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'admin-1',
          email: 'admin@example.com',
          role: 'Admin'
        },
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString()
      })
    });
  });
}

test.describe('RuleSpec versions history', () => {
  test('shows history, diff and allows restoring a version', async ({ page }) => {
    await mockAuth(page, true);

    const historyData = {
      gameId: 'demo-chess',
      totalVersions: 3,
      versions: [
        {
          version: 'v3',
          createdAt: new Date('2025-01-03T09:00:00Z').toISOString(),
          ruleCount: 58,
          createdBy: 'admin@example.com'
        },
        {
          version: 'v2',
          createdAt: new Date('2025-01-02T09:00:00Z').toISOString(),
          ruleCount: 56,
          createdBy: 'editor@example.com'
        },
        {
          version: 'v1',
          createdAt: new Date('2025-01-01T09:00:00Z').toISOString(),
          ruleCount: 52,
          createdBy: 'editor@example.com'
        }
      ]
    };

    const specsByVersion: Record<string, any> = {
      v1: {
        gameId: 'demo-chess',
        version: 'v1',
        createdAt: new Date('2025-01-01T09:00:00Z').toISOString(),
        rules: [{ id: 'rule-1', text: 'Setup pedine' }]
      },
      v2: {
        gameId: 'demo-chess',
        version: 'v2',
        createdAt: new Date('2025-01-02T09:00:00Z').toISOString(),
        rules: [{ id: 'rule-1', text: 'Setup pedine' }, { id: 'rule-2', text: 'Turno giocatore' }]
      },
      v3: {
        gameId: 'demo-chess',
        version: 'v3',
        createdAt: new Date('2025-01-03T09:00:00Z').toISOString(),
        rules: [
          { id: 'rule-1', text: 'Setup pedine' },
          { id: 'rule-2', text: 'Turno giocatore' },
          { id: 'rule-3', text: 'Scacco matto' }
        ]
      }
    };

    const diffResponses: Record<string, any> = {
      'v2->v3': {
        gameId: 'demo-chess',
        fromVersion: 'v2',
        toVersion: 'v3',
        fromCreatedAt: specsByVersion.v2.createdAt,
        toCreatedAt: specsByVersion.v3.createdAt,
        summary: { totalChanges: 3, added: 1, modified: 1, deleted: 0, unchanged: 1 },
        changes: [
          {
            type: 'Added',
            newAtom: 'rule-3',
            newValue: { id: 'rule-3', text: 'Scacco matto', section: 'Finale', page: '12' }
          },
          {
            type: 'Modified',
            newAtom: 'rule-2',
            fieldChanges: [
              { fieldName: 'text', oldValue: 'Turno giocatore', newValue: 'Sequenza turno aggiornata' }
            ]
          },
          {
            type: 'Unchanged',
            oldAtom: 'rule-1',
            oldValue: { id: 'rule-1', text: 'Setup pedine' }
          }
        ]
      },
      'v3->v4': {
        gameId: 'demo-chess',
        fromVersion: 'v3',
        toVersion: 'v4',
        fromCreatedAt: specsByVersion.v3.createdAt,
        toCreatedAt: new Date('2025-01-04T09:00:00Z').toISOString(),
        summary: { totalChanges: 1, added: 0, modified: 0, deleted: 0, unchanged: 1 },
        changes: [
          {
            type: 'Unchanged',
            oldAtom: 'rule-1',
            oldValue: { id: 'rule-1', text: 'Setup pedine' }
          }
        ]
      }
    };

    await page.route(`${apiBase}/games/demo-chess/rulespec/history`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(historyData)
      });
    });

    await page.route(new RegExp(`${apiBase}/games/demo-chess/rulespec/diff.*`), async (route) => {
      const url = new URL(route.request().url());
      const key = `${url.searchParams.get('from')}->${url.searchParams.get('to')}`;
      const diff = diffResponses[key] ?? diffResponses['v2->v3'];
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(diff)
      });
    });

    await page.route(new RegExp(`${apiBase}/games/demo-chess/rulespec/versions/.*`), async (route) => {
      const version = route.request().url().split('/').pop()!;
      const spec = specsByVersion[version];
      if (!spec) {
        await route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ error: 'Not found' }) });
        return;
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(spec)
      });
    });

    await page.route(`${apiBase}/games/demo-chess/rulespec`, async (route) => {
      const restoredSpec = route.request().postDataJSON() as { rules?: unknown[] } & Record<string, any>;
      const createdAt = new Date('2025-01-04T09:00:00Z').toISOString();
      const newSpecVersion = {
        ...restoredSpec,
        version: 'v4',
        createdAt
      };

      historyData.versions = [
        {
          version: newSpecVersion.version,
          createdAt,
          ruleCount: Array.isArray(restoredSpec.rules) ? restoredSpec.rules.length : 0,
          createdBy: 'admin@example.com'
        },
        ...historyData.versions
      ];
      historyData.totalVersions = historyData.versions.length;
      specsByVersion.v4 = newSpecVersion;
      diffResponses['v3->v4'] = {
        ...diffResponses['v3->v4'],
        toCreatedAt: createdAt
      };

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(newSpecVersion)
      });
    });

    await page.addInitScript(() => {
      (window as any).__confirmMessages = [] as string[];
      window.confirm = (message?: string) => {
        (window as any).__confirmMessages.push(String(message ?? ''));
        return true;
      };
    });

    await page.goto('/versions?gameId=demo-chess');

    await expect(page.getByRole('heading', { name: 'Storico Versioni RuleSpec' })).toBeVisible();
    await expect(page.getByText('Game: demo-chess')).toBeVisible();
    await expect(page.getByText('Versioni (3)')).toBeVisible();

    await expect(page.getByText('v3')).toBeVisible();
    await expect(page.getByText('v2')).toBeVisible();
    await expect(page.getByText('v1')).toBeVisible();

    await expect(page.getByText('Modifiche (2)')).toBeVisible();
    await expect(page.getByText('+1')).toBeVisible();
    await expect(page.getByText('~1')).toBeVisible();

    const diffToggle = page.getByLabel('Mostra solo modifiche');
    await diffToggle.click();
    await expect(page.getByText('Modifiche (3)')).toBeVisible();

    const restoreButton = page.getByRole('button', { name: 'Ripristina' }).nth(1);
    await restoreButton.click();

    const confirmMessages = await page.evaluate(() => (window as any).__confirmMessages as string[]);
    expect(confirmMessages.some((message) => message.includes('Sei sicuro di voler ripristinare la versione v2'))).toBeTruthy();

    await page.waitForResponse(`${apiBase}/games/demo-chess/rulespec/versions/v2`);
    await page.waitForResponse(`${apiBase}/games/demo-chess/rulespec`);
    await page.waitForResponse(`${apiBase}/games/demo-chess/rulespec/history`);

    await expect(page.getByText('Versione v2 ripristinata con successo come versione v4')).toBeVisible();
    await expect(page.getByText('Versioni (4)')).toBeVisible();
    await expect(page.getByText('v4')).toBeVisible();
  });

  test('asks the user to authenticate when no session is present', async ({ page }) => {
    await mockAuth(page, false);

    await page.goto('/versions?gameId=demo-chess');

    await expect(page.getByText("Devi effettuare l'accesso per visualizzare lo storico.")).toBeVisible();
    await expect(page.getByRole('link', { name: 'Torna alla home' })).toBeVisible();
  });

  test('displays an error banner when history retrieval fails', async ({ page }) => {
    await mockAuth(page, true);

    await page.route(`${apiBase}/games/demo-chess/rulespec/history`, async (route) => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Server error' })
      });
    });

    await page.goto('/versions?gameId=demo-chess');

    await expect(page.getByText('Impossibile caricare lo storico versioni.')).toBeVisible();
  });
});
