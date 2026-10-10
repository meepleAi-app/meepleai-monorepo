/**
 * Contratto di `AgentDtoSchema` contro i payload che il backend produce davvero (#4154).
 *
 * #4147 ha tolto `Type` da `AgentDto` lato backend, ma lo schema lo esigeva ancora: ogni lista
 * di agenti falliva la validazione, l'assistente in sessione non partiva e la libreria perdeva
 * l'agente di sistema. Le fixture dei test esistenti contenevano `type`, quindi nessun test lo
 * vedeva. L'oracolo qui sotto e' il payload catturato da `GET /api/v1/agents?scope=my-library`
 * (main-dev f8829d954), non un oggetto scritto a mano.
 *
 * La regola che questi test fissano: lo schema esige solo i campi che qualcuno legge, e
 * tollera sia l'assenza di un campo in uscita dal backend sia la sua presenza residua durante
 * un deploy (backend vecchio + frontend nuovo).
 */

import { describe, expect, it } from 'vitest';

import { AgentDtoSchema, GetAllAgentsResponseSchema } from '../agents.schemas';
import { KbDocConsumingAgentSchema } from '../kb-consuming-agents.schemas';

/** Payload reale di `GET /api/v1/agents?scope=my-library`, senza `type` (ADR-094). */
const CURRENT_AGENT = {
  id: '4496601f-060d-48ce-bdf0-5c3567a33acf',
  name: 'Rules Expert',
  strategyName: 'HybridSearch',
  strategyParameters: { TopK: 10, MinScore: 0.55, VectorWeight: 0.7, KeywordWeight: 0.3 },
  isActive: false,
  createdAt: '2026-07-14T16:25:23.206133Z',
  lastInvokedAt: null,
  invocationCount: 0,
  isRecentlyUsed: false,
  isIdle: true,
  isSystemDefined: true,
  gameId: null,
  gameName: null,
  createdByUserId: null,
};

describe('AgentDtoSchema — contratto con il backend (#4154)', () => {
  it('accetta la risposta attuale di GET /api/v1/agents, che non ha piu` `type`', () => {
    const result = GetAllAgentsResponseSchema.safeParse({
      success: true,
      agents: [CURRENT_AGENT],
      count: 1,
    });

    expect(result.success).toBe(true);
  });

  // Nota (#4154): `gamesClient.getAgents` valida un array, ma `GET /api/v1/games/{id}/agents`
  // risponde `{ success, agents, count }` dal 2026-04-18 (#470): un disallineamento di forma
  // indipendente da `type`, che la fetta 0b toglie insieme alla chiamata. Qui si verifica solo che
  // un elenco di agenti senza `type` sia accettato da `AgentDtoSchema.array()`.
  it('accetta un elenco di agenti senza `type` (la forma che `gamesClient.getAgents` si aspetta)', () => {
    expect(AgentDtoSchema.array().safeParse([CURRENT_AGENT]).success).toBe(true);
  });

  it('durante un deploy accetta ancora un backend vecchio che manda `type`, e scarta il campo', () => {
    const result = AgentDtoSchema.safeParse({ ...CURRENT_AGENT, type: 'RAG' });

    expect(result.success).toBe(true);
    expect(result.success && result.data).not.toHaveProperty('type');
  });

  it('tollera l`uscita di `strategyName` e `strategyParameters`, prossima fetta di #4138', () => {
    const { strategyName: _name, strategyParameters: _params, ...withoutStrategy } = CURRENT_AGENT;

    expect(AgentDtoSchema.safeParse(withoutStrategy).success).toBe(true);
  });

  it('resta rumoroso su `isSystemDefined`: la sua assenza non diventa «non di sistema» (#4081)', () => {
    const { isSystemDefined: _flag, ...withoutFlag } = CURRENT_AGENT;

    expect(AgentDtoSchema.safeParse(withoutFlag).success).toBe(false);
  });
});

/** Agente che consuma un documento KB, senza i campi che ADR-094 ritira da AgentDefinition. */
const CONSUMING_AGENT = {
  id: '4496601f-060d-48ce-bdf0-5c3567a33acf',
  name: 'Rules Expert',
  isActive: true,
  status: 'Published',
  isSystemDefined: true,
  typologySlug: null,
  gameName: null,
  invocationCount: 0,
  lastInvokedAt: null,
};

describe('KbDocConsumingAgentSchema — ADR-095 fetta 0', () => {
  it('tollera l`uscita di `gameId`, che ADR-094 ritira da AgentDefinition', () => {
    expect(KbDocConsumingAgentSchema.safeParse(CONSUMING_AGENT).success).toBe(true);
  });

  it('accetta ancora il payload attuale, con `gameId` nullo', () => {
    expect(KbDocConsumingAgentSchema.safeParse({ ...CONSUMING_AGENT, gameId: null }).success).toBe(
      true
    );
  });
});
