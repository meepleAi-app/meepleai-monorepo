/**
 * Gate di architettura — #4999 (ADR-098 §5): gli id d'intento ritirati non tornano nel sorgente.
 *
 * **Il difetto.** Il selettore della chat sceglie un intento fra `auto`, `tutor` e `secca`
 * (`lib/chat/answer-intent.ts`). `arbitro`, `stratega`, `narratore` e `decisore` sono ritirati:
 * l'arbitrato vive nella partita dal vivo (ADR-098 §2). Il game-chat però offriva ancora
 * «Arbitro», con un'unione `'tutor' | 'arbitro'` scritta a mano: premerlo cambiava l'etichetta e
 * nient'altro. Il test di parità di #4404 confronta solo `answer-intent.ts` con `AnswerIntent.cs`,
 * quindi un'unione scritta altrove gli sfugge. È la stessa deriva di #4741 sul selettore desktop.
 *
 * **Cosa controlla.** Sotto `src`, test e story esclusi, un id ritirato in minuscolo come stringa
 * fra apici (`'arbitro'`, `"arbitro"`) o come chiave d'oggetto (`arbitro:`). Le maiuscole contano
 * apposta: «Arbitro» come etichetta resta legittimo nella partita dal vivo. Ogni file che ne
 * contiene deve stare in `EXCEPTIONS`, con il motivo. Un'eccezione rimasta senza occorrenze fa
 * fallire il gate, così l'elenco non invecchia. Un id costruito in altro modo (concatenazione,
 * backtick, maiuscole) non si vede.
 *
 * L'elenco si rigenera con:
 *   git grep -n -E "['\"](arbitro|stratega|narratore|decisore)['\"]|\b(arbitro|stratega|narratore|decisore)\s*:" \
 *     -- 'apps/web/src/*.ts' 'apps/web/src/*.tsx' ':!**\/__tests__/**' ':!**\/*.test.*' ':!**\/*.stories.*'
 */

import { readFileSync } from 'node:fs';
import { join, relative } from 'node:path';

import { sync as globSync } from 'glob';
import { describe, expect, it } from 'vitest';

const SRC = join(process.cwd(), 'src');

const RETIRED_ID =
  /['"](arbitro|stratega|narratore|decisore)['"]|\b(arbitro|stratega|narratore|decisore)\s*:/g;

type Exception = { readonly permanent: string } | { readonly until: string };

/** La pulizia che toglie il codice morto del vecchio sistema multi-agente: seguito di #4404. */
const CLEANUP = '#5001 (chore(chat): pulizia e copy dopo ADR-098)';

const EXCEPTIONS: Readonly<Record<string, Exception>> = {
  'app/(public)/page.tsx': { permanent: '«arbitro» è una parola chiave SEO, non un id' },
  'app/(chat)/chat/page.tsx': { until: `${CLEANUP}: SYSTEM_AGENT_LABELS` },
  'components/ui/meeple/chat-message.tsx': { until: CLEANUP },
  'components/agent/AgentSelectorBadge.tsx': { until: CLEANUP },
  'lib/api/orchestrator-client.ts': { until: CLEANUP },
  'lib/schemas/multi-agent-dashboard-schema.ts': { until: CLEANUP },
  'components/library/AgentConfigForm.tsx': { until: CLEANUP },
};

function sourceFiles(): Map<string, string> {
  const files = globSync('**/*.{ts,tsx}', { cwd: SRC, absolute: true }).filter(
    file => !/[\\/]__tests__[\\/]|\.test\.|\.stories\./.test(file)
  );
  return new Map(
    files.map(file => [relative(SRC, file).replace(/\\/g, '/'), readFileSync(file, 'utf8')])
  );
}

/** Per file, le righe (1-based) con un id ritirato, ciascuna con il testo trovato. */
function retiredIdSites(sources: Map<string, string>): Map<string, string[]> {
  const sites = new Map<string, string[]>();
  for (const [file, source] of sources) {
    const lines = source.split('\n');
    lines.forEach((line, i) => {
      for (const m of line.matchAll(RETIRED_ID)) {
        const list = sites.get(file) ?? [];
        list.push(`${i + 1}: ${m[0]}`);
        sites.set(file, list);
      }
    });
  }
  return sites;
}

describe('gate: gli id d intento ritirati non tornano nel sorgente (#4999, ADR-098 §5)', () => {
  const sites = retiredIdSites(sourceFiles());

  it('vede le occorrenze che conosce', () => {
    // Guardia di non vacuità: se la scansione si rompe, i controlli sotto passerebbero a vuoto. È
    // ancorata all'eccezione permanente, non a una che #5001 toglie.
    expect(sites.get('app/(public)/page.tsx')?.length).toBeGreaterThan(0);
  });

  it('nessun id ritirato fuori dalle eccezioni nominate', () => {
    const undeclared = [...sites]
      .filter(([file]) => EXCEPTIONS[file] === undefined)
      .map(([file, found]) => `${file} → ${found.join(' · ')}`);
    expect(
      undeclared,
      'un intento ritirato offerto di nuovo: usa ANSWER_INTENTS e intentOf di lib/chat/answer-intent.ts. ' +
        "Se il letterale non è un id d'intento, aggiungi un'eccezione con il motivo."
    ).toEqual([]);
  });

  it('nessuna eccezione resta senza occorrenze', () => {
    const stale = Object.keys(EXCEPTIONS).filter(file => !sites.has(file));
    expect(stale, 'eccezione superata: toglila dal gate').toEqual([]);
  });

  it('ogni eccezione dice perché', () => {
    const silent = Object.entries(EXCEPTIONS)
      .filter(([, e]) => ('permanent' in e ? e.permanent : e.until).trim() === '')
      .map(([file]) => file);
    expect(silent).toEqual([]);
  });
});
