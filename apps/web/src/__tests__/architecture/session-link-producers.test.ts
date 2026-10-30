/**
 * Gate di architettura — #4156 (D4, D7): chi costruisce un link a `/sessions/{id}` dichiara da
 * dove viene l'id.
 *
 * **Il difetto.** Le pagine di `/sessions/[id]` leggono una `LiveGameSession`, ma i link verso di
 * esse venivano costruiti con id di tre aggregati diversi: `LiveGameSession`, `GameSession` e la
 * `Session` di `SessionTracking`. Con l'id sbagliato la pagina risponde 404. Il nome della variabile
 * non aiuta: `HomeFeed` e `PlayPanel` scrivevano `session.id` di una `GameSession` e l'inventario
 * fatto a mano li aveva dati per giusti.
 *
 * **Cosa controlla.** Ogni template `/sessions/${…}` scritto nel sorgente, fuori dalle pagine di
 * `sessions/[id]` (che linkano la stessa sessione della rotta) e da `lib/api` (rotte API, non
 * link). Ogni file che ne contiene deve stare in `PRODUCERS`, con una voce per sito, nell'ordine
 * in cui compaiono:
 * - `live` — l'id è quello di una `LiveGameSession`, e la voce dice da dove arriva;
 * - `pending` — l'id è di un altro aggregato, o arriva dal chiamante: la voce dice quale fetta lo
 *   corregge. È l'elenco del debito, e deve solo scendere;
 * - `dead` — nessuno monta il codice. La voce porta un'espressione che deve avere zero occorrenze
 *   nel sorgente, altrimenti il codice non è più morto e va classificato.
 *
 * Un file nuovo, o un sito in più in un file noto, fa fallire il gate finché qualcuno non guarda
 * da dove viene l'id. Il gate non lo verifica: costringe a dichiararlo, dove la review lo vede.
 * Un URL costruito in altro modo (concatenazione, rotta in una costante) non si vede.
 */

import { readFileSync } from 'node:fs';
import { join, relative } from 'node:path';

import { sync as globSync } from 'glob';
import { describe, expect, it } from 'vitest';

const SRC = join(process.cwd(), 'src');

type Site =
  | { readonly live: string }
  | { readonly pending: string }
  | { readonly dead: string; readonly unusedWhen: RegExp };

const D4 = 'GameSessionDto.liveSessionId — la live correlata (D4)';
const LIVE_STORE = 'useLiveSessionStore, valorizzato dalla pagina live';
const DRAWER =
  'liveSessionId della GameSession che il cassetto carica da GET /api/v1/sessions/{id} (D4)';

const PRODUCERS: Readonly<Record<string, readonly Site[]>> = {
  // ── id di una LiveGameSession ──
  'app/(authenticated)/games/[id]/sessions/page.tsx': [{ live: D4 }],
  'app/(authenticated)/players/[id]/sessions/page.tsx': [{ live: D4 }],
  'app/(authenticated)/sessions/_components/SessionsLibraryView.tsx': [
    { live: `SessionListItem.liveSessionId, da ${D4}` },
  ],
  'components/dashboard/session-hero.tsx': [{ live: D4 }],
  'components/features/home/HomeFeed.tsx': [{ live: D4 }],
  'components/features/play/PlayPanel.tsx': [{ live: D4 }],
  'components/library/SessionDrawerSheet.tsx': [{ live: D4 }],
  'components/session/BackToSessionFAB.tsx': [{ live: D4 }],
  'lib/library/hybrid-hub.mappers.ts': [{ live: D4 }],
  'app/(authenticated)/play-records/[id]/edit/page.tsx': [
    { live: 'liveSessionId della partita registrata' },
  ],
  'app/(authenticated)/sessions/new/page.tsx': [
    { live: 'id restituito dal wizard desktop, che crea la live' },
  ],
  'app/(authenticated)/sessions/new/session-wizard-mobile.tsx': [
    { live: 'api.liveSessions.createSession' },
    { live: 'api.liveSessions.createSession' },
  ],
  'app/(authenticated)/sessions/join/page.tsx': [{ live: 'api.liveSessions.getByCode' }],
  'components/dashboard/SessionPanel.tsx': [
    { live: 'store di sessione, che legge api.liveSessions' },
  ],
  'components/layout/AppNav/LiveSessionPill.tsx': [{ live: LIVE_STORE }],
  'components/layout/AppNav/MobileBottomBar.tsx': [{ live: LIVE_STORE }, { live: LIVE_STORE }],
  'components/library/private-game-detail/PrivateGameHub.tsx': [
    { live: 'api.liveSessions.createSession' },
    { live: 'api.liveSessions.getActive' },
  ],
  'components/session/ScoreboardPage.tsx': [
    { live: 'id della rotta /sessions/[id]/scoreboard' },
    { live: 'id della rotta /sessions/[id]/scoreboard' },
  ],
  'components/session/SessionCreationWizard.tsx': [
    { live: 'store di sessione, che crea con api.liveSessions.createSession' },
  ],
  'components/session/StartSessionSheet.tsx': [{ live: 'api.liveSessions.createSession' }],
  'components/ui/data-display/extra-meeple-card/entities/SessionDrawerContent.tsx': [
    { live: DRAWER },
    { live: DRAWER },
    { live: DRAWER },
  ],

  // ── codice che nessuno monta ──
  // Il modulo mobile/drawer lo importa solo un test. Il nome SessionDrawerContent coincide con
  // quello vivo della card estesa, quindi si cerca il percorso del modulo, non il componente.
  'components/layout/mobile/drawer/SessionDrawerContent.tsx': [
    { dead: 'nessuno importa mobile/drawer', unusedWhen: /layout\/mobile\/drawer/ },
    { dead: 'nessuno importa mobile/drawer', unusedWhen: /layout\/mobile\/drawer/ },
  ],
  'app/(authenticated)/dashboard/_components/sections/SessionsTimeline.tsx': [
    { dead: 'nessuno lo monta', unusedWhen: mountOf('SessionsTimeline') },
  ],
  'components/dashboard/incomplete-session-hero.tsx': [
    { dead: 'nessuno lo monta', unusedWhen: mountOf('IncompleteSessionHero') },
  ],
  'components/dashboard/quick-cards-carousel.tsx': [
    { dead: 'nessuno lo monta', unusedWhen: mountOf('QuickCardsCarousel') },
  ],
  'components/session/MeepleResumeSessionCard.tsx': [
    { dead: 'nessuno lo monta', unusedWhen: mountOf('MeepleResumeSessionCard') },
  ],
  'components/session/live/GameOverModal.tsx': [
    { dead: 'nessuno lo monta', unusedWhen: mountOf('GameOverModal') },
  ],
  'lib/navigation/index.ts': [
    { dead: 'sessionDetail non ha chiamanti', unusedWhen: /\bsessionDetail\(/ },
  ],
};

/**
 * Un componente è montato se compare come elemento JSX o come nome importato dinamicamente. Una
 * riesportazione da un `index.ts` non lo monta: per questo non si cercano gli import.
 */
function mountOf(component: string): RegExp {
  return new RegExp(`<${component}\\b|\\.${component}\\b|\\b${component}\\s*\\)`);
}

function sourceFiles(): Map<string, string> {
  const files = globSync('**/*.{ts,tsx}', { cwd: SRC, absolute: true }).filter(
    file => !/[\\/]__tests__[\\/]|\.test\.|\.stories\./.test(file)
  );
  return new Map(
    files.map(file => [relative(SRC, file).replace(/\\/g, '/'), readFileSync(file, 'utf8')])
  );
}

/** Per file, quanti template `/sessions/${…}` costruiscono un link a una pagina di sessione. */
function linkSites(sources: Map<string, string>): Map<string, number> {
  const counts = new Map<string, number>();
  for (const [file, source] of sources) {
    if (file.startsWith('lib/api/')) continue;
    if (file.startsWith('app/(authenticated)/sessions/[id]/')) continue;
    // `/sessions/${` non preceduto da `-` o da una lettera (live-sessions, library/sessions…)
    // né da `/v1` (le rotte API).
    for (const m of source.matchAll(/(?<![-\w])\/sessions\/\$\{/g)) {
      const before = source.slice(Math.max(0, m.index - 8), m.index);
      if (/\/v1$|api$/.test(before)) continue;
      counts.set(file, (counts.get(file) ?? 0) + 1);
    }
  }
  return counts;
}

describe('gate: chi costruisce un link a /sessions/{id} dichiara da dove viene l id (#4156)', () => {
  const sources = sourceFiles();
  const sites = linkSites(sources);

  it('trova i produttori che conosce', () => {
    // Guardia di non vacuità: se la scansione si rompe, i controlli sotto passerebbero a vuoto.
    expect(sites.get('components/session/StartSessionSheet.tsx')).toBe(1);
    expect(sites.size).toBeGreaterThan(10);
  });

  it('ogni file che costruisce il link è classificato, con una voce per sito', () => {
    const problems: string[] = [];
    for (const [file, count] of sites) {
      const declared = PRODUCERS[file];
      if (declared === undefined) {
        problems.push(
          `${file}: ${count} sito/i non classificato/i — da dove viene l'id? ` +
            "Se è di una LiveGameSession aggiungi { live: '<sorgente>' }, altrimenti correggilo (D4, D7)."
        );
      } else if (declared.length !== count) {
        problems.push(`${file}: ${count} siti nel sorgente, ${declared.length} voci nel gate`);
      }
    }
    expect(problems).toEqual([]);
  });

  it('non ha voci per file che non costruiscono più il link', () => {
    const stale = Object.keys(PRODUCERS).filter(file => !sites.has(file));
    expect(stale, 'voce superata: toglila dal gate').toEqual([]);
  });

  it('il codice dichiarato morto è davvero inutilizzato', () => {
    const alive: string[] = [];
    for (const [file, declared] of Object.entries(PRODUCERS)) {
      for (const site of declared) {
        if (!('dead' in site)) continue;
        for (const [other, source] of sources) {
          if (other !== file && site.unusedWhen.test(source)) {
            alive.push(`${file}: ${other} lo usa — non è più morto, classificane i siti`);
          }
        }
      }
    }
    expect(alive).toEqual([]);
  });
});
