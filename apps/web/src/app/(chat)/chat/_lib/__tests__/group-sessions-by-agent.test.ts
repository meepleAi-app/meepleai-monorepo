/**
 * #5001 (ADR-098 §5): la cronologia di /chat nomina i gruppi con le etichette degli intenti.
 *
 * Le etichette erano un elenco a sé, `SYSTEM_AGENT_LABELS`, senza `secca` e con gli id ritirati
 * (`arbitro`, `stratega`, `narratore`): un gruppo `secca` si chiamava «secca», uno `arbitro`
 * «Arbitro». Nel resto dell'app un valore fuori elenco si legge «Auto» (`intentOf`).
 */
import { describe, expect, it } from 'vitest';

import type { ChatSessionSummaryDto } from '@/lib/api/schemas/chat-sessions.schemas';

import { groupSessionsByAgent } from '../group-sessions-by-agent';

function session(
  id: string,
  agentType: string | null,
  lastMessageAt: string
): ChatSessionSummaryDto {
  return {
    id,
    agentId: null,
    agentType,
    agentName: null,
    createdAt: lastMessageAt,
    lastMessageAt,
  } as unknown as ChatSessionSummaryDto;
}

describe('groupSessionsByAgent (#5001)', () => {
  it('un gruppo secca si chiama come l intento nel selettore', () => {
    const [group] = groupSessionsByAgent([session('s1', 'secca', '2026-11-10T10:00:00Z')]);

    expect(group.label).toBe('Risposta secca');
  });

  it.each(['arbitro', 'Stratega', 'narratore', 'decisore'])(
    'un valore ritirato (%s) si mostra come «Auto», insieme alle chat auto',
    retired => {
      const groups = groupSessionsByAgent([
        session('s1', retired, '2026-11-10T10:00:00Z'),
        session('s2', 'auto', '2026-11-09T10:00:00Z'),
      ]);

      expect(groups.map(g => g.label)).toEqual(['Auto']);
      expect(groups[0].sessions.map(s => s.id)).toEqual(['s1', 's2']);
    }
  );

  it('le chat senza agente restano «Chat generali»', () => {
    const [group] = groupSessionsByAgent([session('s1', null, '2026-11-10T10:00:00Z')]);

    expect(group.label).toBe('Chat generali');
  });
});
