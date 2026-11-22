/**
 * #5080: the admin review of share requests, on the screen #5076 restored (`/admin/share-requests` and its detail).
 * The spec it replaces was written for the screen deleted on 2026-02-23: English labels, ids that are not GUIDs, a
 * «My Reviews» popover, a timer and keyboard shortcuts the new screen does not have.
 *
 * The backend is fake and kept in memory (`FakeBackend`, the model of `chat-answer-intent.spec.ts`, #5000): it holds
 * the requests and their review lock, records every request with its query and body, and fails the spec on any
 * request to `/api/v1/` that no route serves. The assertions read the recorded requests, not only the screen.
 *
 * The traps of a fake backend, from #5000: the calls are relative (`**\/api/v1/...`); the session cookie is trusted
 * only with `PLAYWRIGHT_AUTH_BYPASS=true`; the responses must pass the Zod schemas, with valid UUIDs; the PWA service
 * worker dodges `page.route`, so it is blocked.
 */

import { test as base, expect, type Page, type Route } from '@playwright/test';

import { seedCookieConsent } from './_helpers/seedCookieConsent';

/** The check that every request had an answer runs in the teardown, which a skipped test never creates. */
const test = base.extend<{ fakeBackend: (requests: FakeRequest[]) => FakeBackend }>({
  // eslint-disable-next-line no-empty-pattern -- Playwright fixtures destructure their dependencies; this has none
  fakeBackend: async ({}, use) => {
    const created: FakeBackend[] = [];
    await use(requests => {
      const backend = new FakeBackend(requests);
      created.push(backend);
      return backend;
    });
    for (const backend of created) {
      expect(backend.unserved, 'every request to /api/v1/ has a route in the fake backend').toEqual(
        []
      );
    }
  },
});

test.use({ serviceWorkers: 'block' });
test.skip(
  ({ browserName, isMobile }) => browserName !== 'chromium' || isMobile,
  'Chromium desktop only: one run covers the screen'
);

const ADMIN_ID = '44444444-4444-4444-8444-444444444444';
const CONTRIBUTOR_ID = '55555555-5555-4555-8555-555555555555';
const CATAN_ID = '11111111-1111-4111-8111-111111111111';
const PROPOSAL_ID = '22222222-2222-4222-8222-222222222222';
const SHARED_GAME_ID = '33333333-3333-4333-8333-333333333333';
const PRIVATE_GAME_ID = '66666666-6666-4666-8666-666666666666';
const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';
const CREATED_AT = '2026-11-19T10:00:00Z';

type Lock = 'none' | 'mine' | 'theirs';

interface FakeRequest {
  id: string;
  title: string;
  contributionType: 'NewGame' | 'NewGameProposal';
  status: 'Pending' | 'InReview' | 'ChangesRequested' | 'Approved' | 'Rejected';
  lock: Lock;
}

interface RecordedRequest {
  readonly method: string;
  readonly path: string;
  readonly query: URLSearchParams;
  readonly body: unknown;
}

const catan = (lock: Lock = 'none'): FakeRequest => ({
  id: CATAN_ID,
  title: 'Catan',
  contributionType: 'NewGame',
  status: lock === 'none' ? 'Pending' : 'InReview',
  lock,
});

const proposal = (lock: Lock = 'none'): FakeRequest => ({
  id: PROPOSAL_ID,
  title: 'Tile Kingdoms',
  contributionType: 'NewGameProposal',
  status: lock === 'none' ? 'Pending' : 'InReview',
  lock,
});

/** A review lock still held: the panel stops offering decisions once it passes. */
const inTwentyMinutes = () => new Date(Date.now() + 20 * 60_000).toISOString();

class FakeBackend {
  readonly requests: RecordedRequest[] = [];
  readonly unserved: string[] = [];
  private readonly byId: Map<string, FakeRequest & { lockExpiresAt: string | null }>;

  constructor(requests: FakeRequest[]) {
    this.byId = new Map(
      requests.map(r => [
        r.id,
        { ...r, lockExpiresAt: r.lock === 'none' ? null : inTwentyMinutes() },
      ])
    );
  }

  async install(page: Page): Promise<void> {
    await seedCookieConsent(page);
    await page.context().addCookies([
      { name: 'meepleai_session', value: 'fake-session', domain: 'localhost', path: '/' },
      { name: 'meepleai_user_role', value: 'admin', domain: 'localhost', path: '/' },
    ]);
    await page.route('**/api/v1/**', route => this.handle(route));
  }

  /** The requests to one path, in order. */
  sent(method: string, path: string): RecordedRequest[] {
    return this.requests.filter(r => r.method === method && r.path === path);
  }

  private listItem(r: FakeRequest & { lockExpiresAt: string | null }) {
    return {
      id: r.id,
      status: r.status,
      contributionType: r.contributionType,
      sourceGameId: r.contributionType === 'NewGameProposal' ? EMPTY_GUID : SHARED_GAME_ID,
      gameTitle: r.title,
      gameThumbnailUrl: null,
      bggId: null,
      userId: CONTRIBUTOR_ID,
      userName: 'Ada',
      userAvatarUrl: null,
      userTotalContributions: 1,
      userNotes: null,
      attachedDocumentCount: 0,
      createdAt: CREATED_AT,
      isInReview: r.lock !== 'none',
      reviewingAdminId: r.lock === 'none' ? null : ADMIN_ID,
      reviewingAdminName:
        r.lock === 'theirs' ? 'Grace' : r.lock === 'mine' ? 'Admin di prova' : null,
      reviewStartedAt: r.lock === 'none' ? null : CREATED_AT,
      reviewLockExpiresAt: r.lockExpiresAt,
      canStartReview: r.lock === 'none',
      targetSharedGameId: null,
      targetSharedGameTitle: null,
    };
  }

  private details(r: FakeRequest & { lockExpiresAt: string | null }) {
    return {
      id: r.id,
      status: r.status,
      contributionType: r.contributionType,
      sourceGame: {
        id: r.contributionType === 'NewGameProposal' ? PRIVATE_GAME_ID : SHARED_GAME_ID,
        title: r.title,
        yearPublished: 2021,
        description: 'Un gioco di tessere.',
        thumbnailUrl: null,
        bggId: null,
        minPlayers: 2,
        maxPlayers: 4,
        playingTime: 45,
        complexity: null,
        categories: [],
        mechanisms: [],
      },
      targetSharedGame: null,
      contributor: {
        userId: CONTRIBUTOR_ID,
        userName: 'Ada',
        avatarUrl: null,
        joinedAt: '2026-01-01T00:00:00Z',
        totalContributions: 1,
        approvedContributions: 0,
        approvalRate: 0,
        badges: [],
      },
      userNotes: 'Aggiungetelo, per favore',
      attachedDocuments: [],
      history: [],
      lockStatus: {
        isLocked: r.lock !== 'none',
        isLockedByCurrentAdmin: r.lock === 'mine',
        lockedByAdminId: r.lock === 'none' ? null : ADMIN_ID,
        lockedByAdminName:
          r.lock === 'theirs' ? 'Grace' : r.lock === 'mine' ? 'Admin di prova' : null,
        lockExpiresAt: r.lockExpiresAt,
      },
      createdAt: CREATED_AT,
      resolvedAt:
        r.status === 'Approved' || r.status === 'Rejected' ? '2026-11-19T11:00:00Z' : null,
      coverChange: null,
    };
  }

  private resolve(
    r: FakeRequest & { lockExpiresAt: string | null },
    status: FakeRequest['status']
  ) {
    r.status = status;
    r.lock = 'none';
    r.lockExpiresAt = null;
  }

  private async handle(route: Route): Promise<void> {
    const request = route.request();
    const url = new URL(request.url());
    const method = request.method();
    let body: unknown = null;
    try {
      body = request.postDataJSON();
    } catch {
      body = request.postData();
    }
    this.requests.push({ method, path: url.pathname, query: url.searchParams, body });

    const json = (status: number, payload: unknown) =>
      route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(payload) });
    const at = (m: string, path: string) => method === m && url.pathname === path;
    const base = '/api/v1/admin/share-requests';

    if (at('GET', '/api/v1/auth/me')) {
      return json(200, {
        user: {
          id: ADMIN_ID,
          email: 'admin@meepleai.dev',
          displayName: 'Admin di prova',
          role: 'Admin',
          onboardingCompleted: true,
          onboardingSkipped: false,
          emailVerified: true,
        },
        expiresAt: '2099-01-01T00:00:00Z',
      });
    }
    if (at('GET', '/api/v1/auth/session/status')) {
      return json(200, {
        expiresAt: '2099-01-01T00:00:00Z',
        lastSeenAt: null,
        remainingMinutes: 60,
      });
    }
    if (at('GET', '/api/v1/status-banner')) {
      return route.fulfill({ status: 204 });
    }
    // The PDF notifier of the admin layout (PdfProcessingNotifier): not under test. A 204 tells an EventSource to stop.
    if (at('GET', '/api/v1/admin/queue/stream')) {
      return route.fulfill({ status: 204 });
    }

    if (at('GET', base)) {
      const status = url.searchParams.get('status');
      const type = url.searchParams.get('type');
      const search = url.searchParams.get('search')?.toLowerCase();
      const items = [...this.byId.values()]
        .filter(r => !status || r.status === status)
        .filter(r => !type || r.contributionType === type)
        .filter(r => !search || r.title.toLowerCase().includes(search))
        .map(r => this.listItem(r));
      return json(200, { items, total: items.length, page: 1, pageSize: 20 });
    }

    const match = /^\/api\/v1\/admin\/share-requests\/([0-9a-f-]{36})(?:\/([a-z-]+))?$/.exec(
      url.pathname
    );
    const target = match ? this.byId.get(match[1]) : undefined;
    if (match && target) {
      const action = match[2];
      if (method === 'GET' && !action) {
        return json(200, this.details(target));
      }
      if (method === 'POST' && action === 'start-review') {
        target.lock = 'mine';
        target.status = 'InReview';
        target.lockExpiresAt = inTwentyMinutes();
        return json(200, { shareRequestId: target.id, lockExpiresAt: target.lockExpiresAt });
      }
      if (method === 'POST' && action === 'release') {
        target.lock = 'none';
        target.status = 'Pending';
        target.lockExpiresAt = null;
        return route.fulfill({ status: 204 });
      }
      if (method === 'POST' && action === 'approve') {
        this.resolve(target, 'Approved');
        return json(200, {
          shareRequestId: target.id,
          status: 'Approved',
          targetSharedGameId: SHARED_GAME_ID,
          resolvedAt: '2026-11-19T11:00:00Z',
        });
      }
      if (method === 'POST' && action === 'approve-game-proposal') {
        this.resolve(target, 'Approved');
        return json(200, {
          shareRequestId: target.id,
          status: 'Approved',
          targetSharedGameId: SHARED_GAME_ID,
          resolvedAt: '2026-11-19T11:00:00Z',
        });
      }
      if (method === 'POST' && action === 'reject') {
        this.resolve(target, 'Rejected');
        return route.fulfill({ status: 204 });
      }
      if (method === 'POST' && action === 'request-changes') {
        this.resolve(target, 'ChangesRequested');
        return route.fulfill({ status: 204 });
      }
    }

    this.unserved.push(`${method} ${url.pathname}${url.search}`);
    return json(404, { error: 'not served by the fake backend' });
  }
}

/** The query of a request to the list, or undefined for any other request. */
const listQuery = (href: string): URLSearchParams | undefined => {
  const url = new URL(href);
  return url.pathname === '/api/v1/admin/share-requests' ? url.searchParams : undefined;
};

async function openQueue(page: Page, backend: FakeBackend): Promise<void> {
  await backend.install(page);
  await page.goto('/admin/share-requests');
  // The first visit compiles the page under `next dev`.
  await expect(page.getByRole('columnheader', { name: 'Gioco' })).toBeVisible({ timeout: 90_000 });
}

async function openReview(page: Page, backend: FakeBackend, id: string): Promise<void> {
  await backend.install(page);
  await page.goto(`/admin/share-requests/${id}`);
  await expect(page.getByText('Revisione', { exact: true })).toBeVisible({ timeout: 90_000 });
}

test.describe('Admin share requests (#5076 screen)', () => {
  test('the queue lists the requests and sends its filters to the backend', async ({
    page,
    fakeBackend,
  }) => {
    const backend = fakeBackend([catan(), proposal()]);
    await openQueue(page, backend);

    for (const header of ['Gioco', 'Tipo', 'Contributore', 'Stato', 'Creata il']) {
      await expect(page.getByRole('columnheader', { name: header })).toBeVisible();
    }
    const rows = page.locator('table tbody tr');
    await expect(rows).toHaveCount(2);

    // The list moves through loading states between filters: wait for the request that carries the filter, then
    // read the rows its answer leaves on the screen.
    const byType = page.waitForRequest(r => listQuery(r.url())?.get('type') === 'NewGameProposal');
    await page.getByRole('combobox', { name: 'Tipo' }).click();
    await page.getByRole('option', { name: 'Proposta di gioco privato' }).click();
    await byType;
    await expect(rows).toHaveCount(1);
    await expect(rows.first()).toContainText('Tile Kingdoms');

    await page.getByRole('combobox', { name: 'Tipo' }).click();
    await page.getByRole('option', { name: 'Tutti i tipi' }).click();
    await expect(rows).toHaveCount(2);

    const bySearch = page.waitForRequest(r => listQuery(r.url())?.get('search') === 'Catan');
    await page.getByLabel('Cerca per gioco o note').fill('Catan');
    const searched = listQuery((await bySearch).url());
    expect(searched?.get('pageNumber')).toBe('1');
    expect(searched?.get('type')).toBeNull();
    await expect(rows).toHaveCount(1);
    await expect(rows.first()).toContainText('Catan');
  });

  test('a row opens the review of its request', async ({ page, fakeBackend }) => {
    const backend = fakeBackend([catan()]);
    await openQueue(page, backend);

    await page.getByRole('link', { name: 'Catan' }).click();

    await expect(page).toHaveURL(`/admin/share-requests/${CATAN_ID}`);
    await expect(page.getByRole('button', { name: 'Avvia la revisione' })).toBeVisible();
  });

  test('the review is taken, then released', async ({ page, fakeBackend }) => {
    const backend = fakeBackend([catan()]);
    await openReview(page, backend, CATAN_ID);

    await page.getByRole('button', { name: 'Avvia la revisione' }).click();
    await expect(page.getByRole('button', { name: 'Rilascia' })).toBeVisible();
    expect(
      backend.sent('POST', `/api/v1/admin/share-requests/${CATAN_ID}/start-review`)
    ).toHaveLength(1);

    await page.getByRole('button', { name: 'Rilascia' }).click();
    await expect(page.getByRole('button', { name: 'Avvia la revisione' })).toBeVisible();
    expect(backend.sent('POST', `/api/v1/admin/share-requests/${CATAN_ID}/release`)).toHaveLength(
      1
    );
  });

  test('an approval sends the admin notes', async ({ page, fakeBackend }) => {
    const backend = fakeBackend([catan('mine')]);
    await openReview(page, backend, CATAN_ID);

    await page.getByLabel("Note dell'admin (facoltative)").fill('Benvenuto nel catalogo');
    await page.getByRole('button', { name: 'Approva', exact: true }).click();

    await expect(page.getByRole('button', { name: 'Approva', exact: true })).toHaveCount(0);
    expect(
      backend.sent('POST', `/api/v1/admin/share-requests/${CATAN_ID}/approve`).map(r => r.body)
    ).toEqual([{ adminNotes: 'Benvenuto nel catalogo' }]);
  });

  test('a rejection sends its reason', async ({ page, fakeBackend }) => {
    const backend = fakeBackend([catan('mine')]);
    await openReview(page, backend, CATAN_ID);

    await page.getByLabel('Motivo del rifiuto').fill('È già nel catalogo con un altro nome.');
    await page.getByRole('button', { name: 'Rifiuta' }).click();

    await expect(page.getByRole('button', { name: 'Rifiuta' })).toHaveCount(0);
    expect(
      backend.sent('POST', `/api/v1/admin/share-requests/${CATAN_ID}/reject`).map(r => r.body)
    ).toEqual([{ reason: 'È già nel catalogo con un altro nome.' }]);
  });

  test('changes are asked with their feedback', async ({ page, fakeBackend }) => {
    const backend = fakeBackend([catan('mine')]);
    await openReview(page, backend, CATAN_ID);

    await page.getByLabel('Cosa deve cambiare').fill('Serve la scheda del regolamento.');
    await page.getByRole('button', { name: 'Chiedi modifiche' }).click();

    await expect(page.getByRole('button', { name: 'Chiedi modifiche' })).toHaveCount(0);
    expect(
      backend
        .sent('POST', `/api/v1/admin/share-requests/${CATAN_ID}/request-changes`)
        .map(r => r.body)
    ).toEqual([{ feedback: 'Serve la scheda del regolamento.' }]);
  });

  test("a request under another admin's review offers no decision", async ({
    page,
    fakeBackend,
  }) => {
    const backend = fakeBackend([catan('theirs')]);
    await openReview(page, backend, CATAN_ID);

    await expect(page.getByText(/In revisione da Grace/)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Avvia la revisione' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Approva', exact: true })).toHaveCount(0);
  });

  test('a proposal is approved as a new game', async ({ page, fakeBackend }) => {
    const backend = fakeBackend([proposal('mine')]);
    await openReview(page, backend, PROPOSAL_ID);

    await page.getByRole('button', { name: 'Approva la proposta' }).click();

    await expect(page.getByRole('button', { name: 'Approva la proposta' })).toHaveCount(0);
    const sent = backend.sent(
      'POST',
      `/api/v1/admin/share-requests/${PROPOSAL_ID}/approve-game-proposal`
    );
    expect(sent).toHaveLength(1);
    expect(sent[0].body).toMatchObject({ approvalAction: 'ApproveAsNew' });
    expect(
      backend.sent('POST', `/api/v1/admin/share-requests/${PROPOSAL_ID}/approve`)
    ).toHaveLength(0);
  });
});
