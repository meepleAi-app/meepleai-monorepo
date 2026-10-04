/**
 * Semina le condivisioni e i codici di ingresso che il crawler dell'audit non poteva risolvere.
 *
 * #4056. Cinque rotte di condivisione e due di join — `/invites/[token]`,
 * `/game-nights/shared/[token]`, `/join/[token]`, `/play-records/shared/[token]`,
 * `/library/shared/[token]`, `/join/event/[code]`, `/join/session/[code]` — venivano saltate perché
 * nessuna delle colonne sorgente conteneva un valore: non un errore di query, semplicemente nessuno
 * aveva mai creato una condivisione nello stack locale.
 *
 * Si usano le API del prodotto e non INSERT a mano, per due ragioni: il token lo genera il dominio
 * (formato, lunghezza, scadenza) e replicarlo in SQL lo farebbe divergere in silenzio; e il seeding
 * esercita quei flussi, quindi un endpoint di condivisione rotto si manifesta qui invece di
 * nascondersi dietro un salto.
 *
 * 🔴 Due valori non vengono dal database. `play_records."ShareToken"` e gli altri token sì, ma i
 * token di **invito utente** no: `invitation_tokens` conserva solo `token_hash`, e il valore in
 * chiaro esiste unicamente nella risposta di `POST /api/v1/admin/invitations`. Quel token non si
 * semina qui — nessuna rotta dell'inventario lo consuma come segmento — ed è il motivo per cui la
 * mappa, in generale, non può essere soltanto il risultato di query SQL: vedi la nota in fondo.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

const API = process.env.PLAYWRIGHT_API_BASE ?? 'http://localhost:8080';

type Creds = { email: string; password: string };

/** Una sessione autenticata: il cookie che il backend ha emesso al login. */
type Session = { cookie: string; userId: string };

function requireCreds(role: 'USER' | 'ADMIN'): Creds {
  const email = process.env[`AUDIT_${role}_EMAIL`];
  const password = process.env[`AUDIT_${role}_PASSWORD`];
  if (!email || !password) {
    throw new Error(
      `AUDIT_${role}_EMAIL e AUDIT_${role}_PASSWORD non impostate: il seeding delle condivisioni ` +
        `richiede un login reale (le stesse credenziali che usa e2e/audit/auth-setup.ts).`
    );
  }
  return { email, password };
}

async function login({ email, password }: Creds): Promise<Session> {
  const res = await fetch(`${API}/api/v1/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });
  if (!res.ok) {
    throw new Error(`login di ${email} fallito: HTTP ${res.status} ${await res.text()}`);
  }

  // `getSetCookie` restituisce tutti i Set-Cookie separati; concatenarli con '; ' dà l'header
  // Cookie da rimandare. Senza di questo ogni chiamata successiva sarebbe anonima e risponderebbe
  // 401 — un fallimento che si legge come "endpoint rotto" se non si guarda lo status.
  const cookie = res.headers
    .getSetCookie()
    .map(c => c.split(';')[0])
    .join('; ');
  if (!cookie) throw new Error(`login di ${email} non ha emesso alcun cookie di sessione`);

  const body = (await res.json()) as { user?: { id?: string } };
  return { cookie, userId: body.user?.id ?? '' };
}

/** POST autenticata che fallisce con il corpo della risposta, non con un numero nudo. */
async function post<T>(session: Session, path: string, body?: unknown): Promise<T> {
  const res = await fetch(`${API}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Cookie: session.cookie },
    body: JSON.stringify(body ?? {}),
  });
  const text = await res.text();
  if (!res.ok) {
    throw new Error(`POST ${path} -> HTTP ${res.status}: ${text.slice(0, 400)}`);
  }
  return (text ? JSON.parse(text) : undefined) as T;
}

/** Un gioco del catalogo, per le entità che ne richiedono uno. */
async function anySharedGameId(session: Session): Promise<string | undefined> {
  const res = await fetch(`${API}/api/v1/shared-games?page=1&pageSize=1`, {
    headers: { Cookie: session.cookie },
  });
  if (!res.ok) return undefined;
  const body = (await res.json()) as { items?: Array<{ id?: string }> };
  return body.items?.[0]?.id;
}

/**
 * Crea le entità e restituisce i valori catturati.
 *
 * Ogni passo è indipendente: un endpoint rotto lascia il suo parametro assente — e il crawler
 * tornerà a saltare quelle rotte, dichiarando il motivo — invece di far fallire tutto il seeding.
 * Il messaggio dell'errore finisce in `failures`, così chi legge sa quale flusso ha ceduto.
 */
export async function seedShareParams(): Promise<{
  params: Record<string, string>;
  failures: string[];
}> {
  const params: Record<string, string> = {};
  const failures: string[] = [];
  const stamp = new Date()
    .toISOString()
    .replace(/[^0-9]/g, '')
    .slice(0, 14);

  // Solo l'utente semplice: tutto ciò che si semina qui è roba sua (la sua libreria, le sue
  // partite, le sue serate). Serviva anche una sessione admin per l'invito utente, che non si
  // crea più — vedi la nota in fondo.
  const user = await login(requireCreds('USER'));

  const step = async (name: string, fn: () => Promise<void>) => {
    try {
      await fn();
    } catch (err) {
      failures.push(`${name}: ${err instanceof Error ? err.message : String(err)}`);
    }
  };

  await step('libraryShareToken', async () => {
    const dto = await post<{ shareToken: string }>(user, '/api/v1/library/share', {
      privacyLevel: 'public',
      includeNotes: false,
    });
    params.libraryShareToken = dto.shareToken;
  });

  await step('playRecordShareToken', async () => {
    const gameId = await anySharedGameId(user);
    const recordId = await post<string>(user, '/api/v1/play-records', {
      gameId,
      gameName: `Audit seed ${stamp}`,
      sessionDate: new Date().toISOString(),
      // `visibility` è un enum serializzato come numero: una stringa ("Public") fa rifiutare
      // il corpo con 400 prima di arrivare all'handler.
      visibility: 0,
    });
    const dto = await post<{ shareToken: string }>(user, `/api/v1/play-records/${recordId}/share`);
    params.playRecordShareToken = dto.shareToken;
  });

  let gameNightId: string | undefined;
  await step('gameNight', async () => {
    gameNightId = await post<string>(user, '/api/v1/game-nights', {
      title: `Audit seed ${stamp}`,
      // 🔴 Deve essere UTC (`Z`). Un offset diverso da zero fa rispondere 500: Npgsql rifiuta
      // un DateTimeOffset non-UTC su `timestamptz`, benché il contratto dichiari DateTimeOffset.
      // Difetto tracciato in #4055 — quando è chiuso, questo commento può cadere.
      scheduledAt: new Date(Date.now() + 30 * 24 * 3600 * 1000).toISOString(),
      location: 'Audit seed',
      maxPlayers: 6,
    });
  });

  await step('gameNightShareToken', async () => {
    if (!gameNightId) throw new Error('nessun game night creato nel passo precedente');
    const dto = await post<{ shareToken: string }>(
      user,
      `/api/v1/game-nights/${gameNightId}/share-token`
    );
    params.gameNightShareToken = dto.shareToken;
  });

  await step('gameNightInviteToken', async () => {
    if (!gameNightId) throw new Error('nessun game night creato nel passo precedente');
    // Email sempre nuova: un invito pendente per lo stesso indirizzo fa rispondere 409.
    const dto = await post<{ token: string }>(
      user,
      `/api/v1/game-nights/${gameNightId}/invitations`,
      { email: `audit-invitee-${stamp}@meepleai.test` }
    );
    params.gameNightInviteToken = dto.token;
  });

  await step('liveSessionCode', async () => {
    // Il codice nasce alla creazione (`LiveGameSession.Create`), non all'avvio: non serve
    // far partire la sessione per avere un codice navigabile.
    await post<string>(user, '/api/v1/live-sessions', { gameName: `Audit seed ${stamp}` });
    // Il codice non è nel corpo della risposta (che è il solo id): lo legge la query SQL di
    // `PARAM_QUERIES.liveSessionCode`, che prende la sessione più recente.
  });

  // Nessun invito utente. `POST /api/v1/admin/invitations` restituisce il token in chiaro — il
  // database conserva solo `token_hash` — ma nessuna rotta dell'inventario lo consuma come
  // segmento: arriva a `/setup-account?token=…` come query param, e il crawler percorre path
  // senza query string. Crearlo a ogni esecuzione lascerebbe un utente `Pending` nel database
  // per un valore che non risolve niente. Quando l'inventario coprirà le rotte con query param,
  // il passo va riaggiunto — è l'unico token che il DB non può restituire.

  return { params, failures };
}
