/**
 * #5000 (ADR-098 §5): the answer intent in the browser — selector, PATCH, reload, and the read-only intent on the
 * phone. The deterministic part of the #4404 browser check; the SHAPE of the answer under each intent needs a real
 * model and lives in #4471.
 *
 * The backend is fake and kept in memory (`FakeBackend`): it holds one thread, records every request with its body,
 * and fails the spec on any request to `/api/v1/` that no route serves. The assertions read the recorded bodies, not
 * only the screen.
 *
 * Three traps that make a spec with a fake backend pass for nothing (see #5000):
 * - in the browser `getApiBase()` is `''`, so the calls are relative: the routes use `**\/api/v1/...`, never an
 *   absolute base (the absolute routes of `AuthHelper.mockAuthenticatedSession` do not fire for browser calls);
 * - the session cookie is trusted only with `PLAYWRIGHT_AUTH_BYPASS=true`, which the `webServer` of
 *   `playwright.config.ts` sets; a `next dev` started by hand needs it explicitly;
 * - the responses must pass the Zod schemas, with valid UUIDs (the PATCH route is `{threadId:guid}`).
 *
 * A fourth one, met writing this spec: the PWA service worker. `page.route` does not see the requests it makes, so
 * they reach the real server and dodge the «no request without an answer» check. Seen as two GETs of the thread
 * answering 401 — from the server, not from the fake. `serviceWorkers: 'block'` keeps every request on the page.
 *
 * `/chat/[threadId]` mounts `ChatMobile` and `ChatThreadView` together, one hidden by CSS, each with its own loads:
 * several GETs of the thread per visit (doubled by StrictMode in development) are not a defect of the proof.
 */

import { test as base, expect, type Page, type Route, type TestInfo } from '@playwright/test';

import { seedCookieConsent } from './_helpers/seedCookieConsent';

/**
 * `fakeBackend(...)` builds the fake of a test. The check that every request had an answer runs in the teardown of
 * the fixture, which a skipped test never creates. An `afterEach` ran on the skipped tests too — the skip below reads
 * `isMobile`, a test fixture, so Playwright runs it as a `beforeEach` — and read a fake that did not exist: every
 * project but desktop Chromium failed with a `TypeError` (review of #5061).
 */
const test = base.extend<{
  fakeBackend: (agentType: string | null, messages?: ThreadState['messages']) => FakeBackend;
}>({
  // eslint-disable-next-line no-empty-pattern -- Playwright fixtures destructure their dependencies; this has none
  fakeBackend: async ({}, use) => {
    const created: FakeBackend[] = [];
    await use((agentType, messages = []) => {
      const backend = new FakeBackend(agentType, messages);
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
// The spec sets its own viewports (1280 px and 375 px): one run on desktop Chromium, not one per device project.
test.skip(
  ({ browserName, isMobile }) => browserName !== 'chromium' || isMobile,
  'Chromium desktop only: the spec sets its own viewports'
);

const THREAD_ID = '8d6f1a2e-3b4c-4d5e-8f60-718293a4b5c6';
const GAME_ID = '1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d';
const USER_ID = 'f0e1d2c3-b4a5-4968-8776-655443322110';
const LIBRARY_ENTRY_ID = '2b3c4d5e-6f70-4182-9a3b-4c5d6e7f8091';
const DOCUMENT_ID = '3c4d5e6f-7081-4293-8b4c-5d6e7f809102';
const NOW = '2026-11-18T10:00:00Z';

/** The StreamingEventType ids of `qaStream` (`QA_EVENT_TYPES`). */
const EVENT = { TOKEN: 7, COMPLETE: 4, CONTINUATION_AVAILABLE: 29 } as const;

interface RecordedRequest {
  readonly method: string;
  readonly path: string;
  readonly body: unknown;
}

interface ThreadState {
  agentType: string | null;
  messages: Array<{ role: string; content: string; timestamp: string }>;
}

class FakeBackend {
  readonly requests: RecordedRequest[] = [];
  readonly unserved: string[] = [];
  readonly thread: ThreadState;
  /** The next PATCH of the intent answers 500 and changes nothing. */
  failNextIntentChange = false;

  constructor(agentType: string | null, messages: ThreadState['messages'] = []) {
    this.thread = { agentType, messages };
  }

  async install(page: Page): Promise<void> {
    // The consent banner covers the bottom of every page: answered before the first load.
    await seedCookieConsent(page);
    await page.context().addCookies([
      { name: 'meepleai_session', value: 'fake-session', domain: 'localhost', path: '/' },
      { name: 'meepleai_user_role', value: 'user', domain: 'localhost', path: '/' },
    ]);
    await page.route('**/api/v1/**', route => this.handle(route));
  }

  /** The bodies of the requests to one path, in order. */
  bodiesOf(method: string, path: string): unknown[] {
    return this.requests.filter(r => r.method === method && r.path === path).map(r => r.body);
  }

  private threadDto() {
    return {
      id: THREAD_ID,
      gameId: GAME_ID,
      privateGameId: null,
      agentId: null,
      agentType: this.thread.agentType,
      title: 'Catan',
      createdAt: NOW,
      lastMessageAt: this.thread.messages.length > 0 ? NOW : null,
      messageCount: this.thread.messages.length,
      messages: this.thread.messages,
    };
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
    this.requests.push({ method, path: url.pathname, body });

    const json = (status: number, payload: unknown) =>
      route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(payload) });
    const at = (m: string, path: string) => method === m && url.pathname === path;

    if (at('GET', '/api/v1/auth/me')) {
      return json(200, {
        user: {
          id: USER_ID,
          email: 'user@meepleai.dev',
          displayName: 'Utente di prova',
          role: 'User',
          onboardingCompleted: true,
          onboardingSkipped: false,
          emailVerified: true,
        },
        expiresAt: '2099-01-01T00:00:00Z',
      });
    }
    // The shell of the app around the chat: not under test, answered empty and valid for its schema. Named one by
    // one, so that a request nobody expected still fails the spec.
    if (at('GET', '/api/v1/status-banner') || at('GET', '/api/v1/sessions/current')) {
      return route.fulfill({ status: 204 });
    }
    if (at('GET', '/api/v1/sessions/active')) {
      return json(200, { sessions: [], total: 0, page: 1, pageSize: 1 });
    }
    if (at('GET', `/api/v1/users/${USER_ID}/chat-sessions/recent`)) {
      return json(200, []);
    }
    if (at('GET', '/api/v1/auth/session/status')) {
      return json(200, {
        expiresAt: '2099-01-01T00:00:00Z',
        lastSeenAt: null,
        remainingMinutes: 60,
      });
    }
    if (at('GET', '/api/v1/rag/enhancements/estimate')) {
      return json(200, {
        activeEnhancements: '',
        activeFlags: [],
        extraCreditsPerQuery: 0,
        useBalancedAuxModel: false,
      });
    }
    if (at('GET', `/api/v1/games/${GAME_ID}`)) {
      return json(200, {
        id: GAME_ID,
        title: 'Catan',
        publisher: null,
        yearPublished: null,
        minPlayers: null,
        maxPlayers: null,
        minPlayTimeMinutes: null,
        maxPlayTimeMinutes: null,
        bggId: null,
        createdAt: NOW,
      });
    }

    // The page of the game (step 8): the game is in the library and has an indexed rulebook, so the chat tab mounts
    // the game chat. No game books.
    if (at('GET', `/api/v1/library/games/${GAME_ID}`)) {
      return json(200, {
        id: LIBRARY_ENTRY_ID,
        userId: USER_ID,
        gameId: GAME_ID,
        gameTitle: 'Catan',
        gamePublisher: null,
        gameYearPublished: null,
        gameDescription: null,
        gameIconUrl: null,
        gameImageUrl: null,
        minPlayers: null,
        maxPlayers: null,
        playTimeMinutes: null,
        complexityRating: null,
        averageRating: null,
        addedAt: NOW,
        notes: null,
        isFavorite: false,
        currentState: 'Owned',
        stateChangedAt: null,
        stateNotes: null,
        isAvailableForPlay: true,
        timesPlayed: 0,
        lastPlayed: null,
        winRate: null,
        avgDuration: null,
        hasRagAccess: true,
      });
    }
    if (at('GET', `/api/v1/knowledge-base/${GAME_ID}/documents`)) {
      return json(200, [
        {
          id: DOCUMENT_ID,
          title: 'Regolamento',
          status: 'indexed',
          pageCount: 12,
          createdAt: NOW,
          category: 'Rulebook',
          versionLabel: null,
        },
      ]);
    }
    if (at('GET', `/api/v1/shared-games/${GAME_ID}`)) {
      return json(200, {
        id: GAME_ID,
        bggId: null,
        title: 'Catan',
        yearPublished: 1995,
        description: '',
        minPlayers: 3,
        maxPlayers: 4,
        playingTimeMinutes: 90,
        minAge: 10,
        complexityRating: null,
        averageRating: null,
        rules: null,
        status: 'Published',
        createdBy: USER_ID,
        modifiedBy: null,
        createdAt: NOW,
        modifiedAt: null,
        faqs: [],
        erratas: [],
        designers: [],
        publishers: [],
        categories: [],
        mechanics: [],
      });
    }
    // A catalog game, not a private one: the page asks both, and the private route answers what the server would.
    if (at('GET', `/api/v1/private-games/${GAME_ID}`)) {
      return json(404, { error: 'not a private game' });
    }
    if (
      at('GET', '/api/v1/gamebooks') ||
      at('GET', '/api/v1/gamebook/books') ||
      at('GET', `/api/v1/games/${GAME_ID}/session-contributors`)
    ) {
      return json(200, []);
    }

    if (at('GET', `/api/v1/chat-threads/${THREAD_ID}`)) {
      return json(200, this.threadDto());
    }
    if (at('GET', '/api/v1/chat-threads') && url.searchParams.get('gameId') === GAME_ID) {
      // `{ threads, count }`, as `chatClient.getThreadsByGame` reads it: a bare array reads as no thread at all.
      return json(200, { threads: [this.threadDto()], count: 1 });
    }
    if (at('PATCH', `/api/v1/chat-threads/${THREAD_ID}/agent`)) {
      if (this.failNextIntentChange) {
        this.failNextIntentChange = false;
        return json(500, { error: 'injected by the spec' });
      }
      this.thread.agentType = (body as { agentType: string }).agentType;
      return json(200, this.threadDto());
    }
    if (at('POST', `/api/v1/chat-threads/${THREAD_ID}/messages`)) {
      const message = body as { role: string; content: string };
      this.thread.messages.push({ role: message.role, content: message.content, timestamp: NOW });
      return json(200, this.threadDto());
    }
    if (at('POST', '/api/v1/agents/qa/stream')) {
      const request = body as { continuationToken?: string };
      // The first answer stops half way and offers a continuation; the continuation finishes it.
      const events = request.continuationToken
        ? [
            { type: EVENT.TOKEN, data: { token: ' due carte a testa.' } },
            { type: EVENT.COMPLETE, data: { answer: ' due carte a testa.', snippets: [] } },
          ]
        : [
            { type: EVENT.TOKEN, data: { token: 'Ogni giocatore pesca' } },
            { type: EVENT.CONTINUATION_AVAILABLE, data: { continuationToken: 'tok-1' } },
            { type: EVENT.COMPLETE, data: { answer: 'Ogni giocatore pesca', snippets: [] } },
          ];
      return route.fulfill({
        status: 200,
        contentType: 'text/event-stream',
        body: events.map(e => `data: ${JSON.stringify({ ...e, timestamp: NOW })}\n\n`).join(''),
      });
    }

    this.unserved.push(`${method} ${url.pathname}${url.search}`);
    return route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
  }
}

/**
 * A screenshot in the report of the run, for the PR (#5000): nothing is written into the repository. The element the
 * screenshot is about is scrolled into view first: `toBeVisible` does not mean «on screen».
 */
async function snapshot(
  page: Page,
  testInfo: TestInfo,
  name: string,
  subject: ReturnType<Page['locator']>
): Promise<void> {
  await subject.scrollIntoViewIfNeeded();
  const path = testInfo.outputPath(`${name}.png`);
  await page.screenshot({ path });
  await testInfo.attach(name, { path, contentType: 'image/png' });
}

/**
 * #5060: the WCAG contrast ratio of a text element against what is behind it — its own background, composited over
 * the nearest opaque ancestor. Colours are converted by the browser (canvas), not parsed. axe cannot do this here: on
 * the game chat it reports these nodes as «incomplete» (the drawer overlays the page, the page background is a
 * gradient), so a check through axe passed on the 3.64:1 pill.
 */
async function contrastRatio(subject: ReturnType<Page['locator']>): Promise<number> {
  return subject.evaluate(element => {
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = 1;
    const ctx = canvas.getContext('2d', { willReadFrequently: true })!;
    const rgba = (colour: string): number[] => {
      ctx.clearRect(0, 0, 1, 1);
      ctx.fillStyle = colour;
      ctx.fillRect(0, 0, 1, 1);
      const [r, g, b, a] = ctx.getImageData(0, 0, 1, 1).data;
      return [r, g, b, a / 255];
    };
    let base: number[] = [255, 255, 255, 1];
    for (let el = element.parentElement; el; el = el.parentElement) {
      const c = rgba(getComputedStyle(el).backgroundColor);
      if (c[3] > 0.99) {
        base = c;
        break;
      }
    }
    // The element's own background, then each translucent layer below it down to the opaque base.
    const layers: number[][] = [];
    for (let el: Element | null = element; el; el = el.parentElement) {
      const c = rgba(getComputedStyle(el).backgroundColor);
      if (c[3] > 0.99) break;
      if (c[3] > 0) layers.unshift(c);
    }
    const background = layers.reduce(
      (under, [r, g, b, a]) => [
        r * a + under[0] * (1 - a),
        g * a + under[1] * (1 - a),
        b * a + under[2] * (1 - a),
      ],
      base.slice(0, 3)
    );
    const text = rgba(getComputedStyle(element).color);
    const luminance = ([r, g, b]: number[]) =>
      [r, g, b]
        .map(v => v / 255)
        .map(v => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4))
        .reduce((sum, v, i) => sum + v * [0.2126, 0.7152, 0.0722][i], 0);
    const [l1, l2] = [luminance(text), luminance(background)].sort((x, y) => y - x);
    return (l1 + 0.05) / (l2 + 0.05);
  });
}

async function openThread(page: Page, backend: FakeBackend): Promise<void> {
  await backend.install(page);
  await page.goto(`/chat/${THREAD_ID}`);
}

function intentSelector(page: Page) {
  return page.getByRole('combobox', { name: /Tipo di risposta/ });
}

/** The desktop view: `/chat/[threadId]` also mounts `ChatMobile`, hidden by CSS, with the same test ids. */
function desktopView(page: Page) {
  return page.getByTestId('chat-thread-view');
}

async function choose(page: Page, label: string): Promise<void> {
  await intentSelector(page).click();
  await page.getByRole('option', { name: new RegExp(label) }).click();
}

test.describe('the answer intent, on the desktop (#5000)', () => {
  test.use({ viewport: { width: 1280, height: 800 } });

  test('steps 1-3: Auto, then «Risposta secca» goes to the server, and a reload reads it back', async ({
    page,
    fakeBackend,
  }, testInfo) => {
    const backend = fakeBackend(null);
    await openThread(page, backend);
    await expect(intentSelector(page)).toContainText('Auto');

    await choose(page, 'Risposta secca');

    await expect
      .poll(() => backend.bodiesOf('PATCH', `/api/v1/chat-threads/${THREAD_ID}/agent`))
      .toEqual([{ agentType: 'secca' }]);
    await expect(intentSelector(page)).toContainText('Risposta secca');

    await page.reload();
    await expect(intentSelector(page)).toContainText('Risposta secca');
    await expect(intentSelector(page)).not.toContainText('Auto');
    expect(backend.thread.agentType).toBe('secca');
    // One change, one PATCH: the poll above stops at the first match, so the list is read again here.
    expect(backend.bodiesOf('PATCH', `/api/v1/chat-threads/${THREAD_ID}/agent`)).toEqual([
      { agentType: 'secca' },
    ]);
    await snapshot(page, testInfo, 'desktop-after-reload-risposta-secca', intentSelector(page));
  });

  test('steps 4-5: the question and its continuation both carry the thread as chatId', async ({
    page,
    fakeBackend,
  }) => {
    const backend = fakeBackend('tutor');
    await openThread(page, backend);
    await expect(intentSelector(page)).toContainText('Tutor');

    await desktopView(page).getByTestId('message-input').fill('Quante carte si pescano?');
    await desktopView(page).getByTestId('send-btn').click();
    await desktopView(page).getByTestId('continue-button').click();

    await expect.poll(() => backend.bodiesOf('POST', '/api/v1/agents/qa/stream').length).toBe(2);
    const [question, continuation] = backend.bodiesOf('POST', '/api/v1/agents/qa/stream') as Array<
      Record<string, unknown>
    >;
    expect(question).toMatchObject({ chatId: THREAD_ID, query: 'Quante carte si pescano?' });
    expect(continuation).toMatchObject({ chatId: THREAD_ID, continuationToken: 'tok-1' });
  });

  test('step 6: a failed change leaves the selector on the stored intent and says so', async ({
    page,
    fakeBackend,
  }) => {
    const backend = fakeBackend('tutor');
    backend.failNextIntentChange = true;
    await openThread(page, backend);
    await expect(intentSelector(page)).toContainText('Tutor');

    await choose(page, 'Risposta secca');

    await expect(page.getByText('Errore nel cambio del tipo di risposta')).toBeVisible();
    await expect(intentSelector(page)).toContainText('Tutor');
    // The trigger mirrors the whole option, description included: the negative keeps «Tutor» from matching for nothing.
    await expect(intentSelector(page)).not.toContainText('Risposta secca');
    expect(backend.bodiesOf('PATCH', `/api/v1/chat-threads/${THREAD_ID}/agent`)).toEqual([
      { agentType: 'secca' },
    ]);
    expect(backend.thread.agentType).toBe('tutor');
  });
});

test.describe('the answer intent, on the phone (#5000)', () => {
  test.use({ viewport: { width: 375, height: 812 } });

  test('step 7: ChatMobile shows the intent of the thread, read-only', async ({
    page,
    fakeBackend,
  }, testInfo) => {
    const backend = fakeBackend('tutor');
    await openThread(page, backend);

    const intent = page.getByTestId('thread-intent');
    await expect(intent).toBeVisible();
    await expect(intent).toHaveText(/Tutor/);
    // The only control of the header of the thread is «Torna indietro»: nothing to change the intent with. A select is a
    // combobox, not a button, so the other roles of a choice are counted too, and no combobox is on screen anywhere.
    const header = page.locator('header', { has: page.getByTestId('thread-intent') });
    await expect(header.getByRole('button')).toHaveCount(1);
    await expect(header.getByRole('button', { name: 'Torna indietro' })).toBeVisible();
    for (const role of [
      'combobox',
      'listbox',
      'radiogroup',
      'radio',
      'switch',
      'tab',
      'menu',
    ] as const) {
      await expect(header.getByRole(role)).toHaveCount(0);
    }
    await expect(page.getByRole('combobox')).toHaveCount(0);
    await snapshot(page, testInfo, 'phone-chat-intent-read-only', intent);
  });

  test('step 8: the game chat, from the page of the game, shows the intent of the thread and no «Arbitro»', async ({
    page,
    fakeBackend,
  }, testInfo) => {
    const backend = fakeBackend('secca', [
      { role: 'user', content: 'Quante carte si pescano?', timestamp: NOW },
      { role: 'assistant', content: 'Due carte a testa.', timestamp: NOW },
    ]);
    await backend.install(page);
    // #4992: the deep link opens the drawer on the chat, below lg.
    await page.goto(`/library/${GAME_ID}?tab=aiChat`);

    // The desktop layout of the page is mounted too, hidden by CSS: the visible header is the drawer's.
    const header = page.locator('[data-testid="game-chat-header"]:visible');
    await expect(header).toBeVisible();
    // No «Arbitro» anywhere in the page, hidden layouts included: before #4999 the switch sat in the desktop
    // sidebar, mounted under the drawer.
    await expect(page.getByText(/arbitro/i)).toHaveCount(0);
    await expect(header).toHaveAttribute('data-intent', 'secca');
    await expect(header).toContainText('Risposta secca');
    await expect(
      page.getByText('Quante carte si pescano?').filter({ visible: true })
    ).toBeVisible();
    await expect(header.getByRole('button')).toHaveCount(0);
    await snapshot(page, testInfo, 'phone-game-chat-no-arbitro', header);

    // #5060: the intent pill and the name of the assistant are text on a tint of the agent colour. With the base
    // colour they measured 3.64:1 and about 4:1, under AA 4.5:1 for normal text; the text token of the agent colour
    // is meant for this.
    const answer = page.locator('[data-testid="chat-bubble"][data-role="agent"]:visible');
    const pill = header.locator('span:has(> .sr-only)');
    const assistantName = answer.getByText('Assistente', { exact: true });
    await expect(assistantName).toBeVisible();
    expect(await contrastRatio(pill), 'the intent pill, 10 px bold').toBeGreaterThanOrEqual(4.5);
    expect(
      await contrastRatio(assistantName),
      'the name of the assistant, 14 px bold'
    ).toBeGreaterThanOrEqual(4.5);
  });
});
