/**
 * #4155 — il viaggio del possesso, con backend e LLM veri.
 *
 * Un utente normale ha il gioco in libreria senza averne dichiarato il possesso. Chiede in chat:
 * il cancello RAG lo respinge e la chat lo spiega (non «An error occurred»). Dichiara il possesso
 * dal riquadro: la stessa domanda riparte da sola e arriva una risposta con le citazioni.
 *
 * Perché un utente creato dall'admin e non quello seminato: l'utente seminato degli smoke test è
 * l'admin iniziale, e un admin passa sempre il cancello (regola 1 di RagAccessService), quindi con
 * lui il diniego non si può provocare. La registrazione pubblica in CI è chiusa.
 *
 * Stack: `make dev-from-snapshot` (giochi con il manuale indicizzato). Si lancia a mano con il
 * workflow `e2e-smoke-real-llm-weekly.yml`. In locale: SMOKE_USER_EMAIL / SMOKE_USER_PASSWORD di
 * un admin e SMOKE_GAME_ID di un gioco con il manuale indicizzato e RAG non pubblico.
 */
import { test, expect, type APIRequestContext } from '@playwright/test';

import { applySessionToPage, loginAs, smokeLogin } from '../smoke-real-backend/_helpers/auth';

const API_BASE = process.env.SMOKE_API_BASE ?? 'http://localhost:8080';
const SEED_GAME_ID = process.env.SMOKE_GAME_ID ?? '3d54901d-fe9d-4d00-a7ae-e9865bb764f7';
const QUESTION = process.env.SMOKE_OWNERSHIP_QUESTION ?? 'Come si vince la partita?';
const MAILPIT = process.env.SMOKE_MAILPIT_BASE ?? 'http://localhost:8025';

/**
 * Un utente creato dall'admin nasce con l'email non verificata e senza periodo di grazia, e senza
 * verifica non può aggiungere giochi alla libreria (403 «Email verification required»). Non c'è un
 * endpoint admin che la verifichi: si fa come l'utente, con il link dell'email letto da Mailpit.
 */
async function verifyEmailViaMailpit(request: APIRequestContext, email: string): Promise<void> {
  const resend = await request.post(`${API_BASE}/api/v1/auth/email/resend`, { data: { email } });
  expect(resend.ok(), await resend.text()).toBe(true);

  let token: string | null = null;
  await expect
    .poll(
      async () => {
        const search = await request.get(
          `${MAILPIT}/api/v1/search?query=${encodeURIComponent(`to:"${email}"`)}`
        );
        const { messages = [] } = (await search.json()) as { messages?: Array<{ ID: string }> };
        for (const { ID } of messages) {
          const message = await request.get(`${MAILPIT}/api/v1/message/${ID}`);
          const { HTML = '', Text = '' } = (await message.json()) as {
            HTML?: string;
            Text?: string;
          };
          const match = /verify-email\?token=([^"&\s<]+)/.exec(HTML + Text);
          if (match) token = decodeURIComponent(match[1]);
        }
        return token;
      },
      { timeout: 30_000, message: 'email di verifica in Mailpit' }
    )
    .not.toBeNull();

  const verify = await request.post(`${API_BASE}/api/v1/auth/email/verify`, { data: { token } });
  expect(verify.ok(), await verify.text()).toBe(true);
}

test.describe.configure({ retries: 2 });

test.describe('SMOKE-REAL-LLM — dichiarazione di possesso al primo diniego (#4155)', () => {
  test('@flaky-by-design la chat respinge, offre la dichiarazione e poi risponde con le citazioni', async ({
    page,
    request,
  }) => {
    test.setTimeout(180_000);

    // 1. L'admin seminato crea un utente normale, e la sessione diventa la sua.
    const admin = await smokeLogin(request);
    const email = `ownership-${Date.now()}@meepleai.test`;
    const password = 'Ownership4155!Strong';
    const created = await request.post(`${API_BASE}/api/v1/admin/users`, {
      headers: { Cookie: admin.cookieHeaders.join('; ') },
      data: { email, password, displayName: 'Ownership 4155', role: 'user' },
    });
    expect(created.status(), await created.text()).toBe(201);
    await verifyEmailViaMailpit(request, email);

    const user = await loginAs(request, email, password);
    const userCookies = { Cookie: user.cookieHeaders.join('; ') };
    await applySessionToPage(page, user.cookieHeaders);

    // 2. Il gioco in libreria, senza dichiarazione, e un thread di chat su quel gioco.
    const added = await request.post(`${API_BASE}/api/v1/library/games/${SEED_GAME_ID}`, {
      headers: userCookies,
      data: {},
    });
    expect(added.status(), await added.text()).toBe(201);
    const thread = await request.post(`${API_BASE}/api/v1/chat-threads`, {
      headers: userCookies,
      data: { gameId: SEED_GAME_ID, title: 'Possesso (#4155)' },
    });
    expect(thread.status(), await thread.text()).toBe(201);
    const { id: threadId } = (await thread.json()) as { id: string };

    // 3. La domanda: il cancello respinge, e la chat spiega invece di mostrare un guasto.
    await page.goto(`/chat/${threadId}`, { waitUntil: 'domcontentloaded' });
    const input = page.locator('[data-testid="message-input"]:visible').first();
    await expect(input).toBeVisible({ timeout: 30_000 });
    await input.fill(QUESTION);
    await page.locator('[data-testid="send-btn"]:visible').first().click();

    const notice = page.locator('[data-testid="rag-ownership-required"]:visible').first();
    await expect(notice).toBeVisible({ timeout: 60_000 });
    await expect(notice).toContainText('copyright');
    await expect(page.getByText(/An error occurred/)).toHaveCount(0);

    // 4. La dichiarazione dal riquadro: la stessa domanda riparte da sola.
    await notice.getByRole('button', { name: /Dichiara di possedere/ }).click();
    await page.getByTestId('ownership-checkbox').click();
    await page.getByRole('button', { name: 'Conferma Possesso' }).click();
    await expect(notice).toBeHidden({ timeout: 30_000 });

    // 5. La risposta arriva, con le citazioni, e la domanda non è stata salvata due volte.
    await expect(page.locator('[data-testid^="citation-chip-"]').first()).toBeVisible({
      timeout: 120_000,
    });
    await expect(page.getByTestId('message-user')).toHaveCount(1);
    await expect(page.getByTestId('message-assistant')).toHaveCount(1);
  });
});
