/**
 * #4093 — lo schema deve accettare la risposta REALE del backend.
 *
 * Il fixture qui sotto è la risposta verbatim di
 * `GET /api/v1/admin/ai-models?status=active` contro lo stack locale, catturata con curl.
 * Non è derivato da `PagedAiModelsSchema`: derivarlo dallo schema che si vuole verificare
 * renderebbe il test vacuo, ed è esattamente il modo in cui questo contratto è rimasto
 * disallineato per mesi senza che nulla diventasse rosso (#4059 voce 4).
 */

import { describe, it, expect } from 'vitest';

import { AiModelDtoSchema, PagedAiModelsSchema } from '../ai-models.schemas';

/** Risposta verbatim del backend. NON modificare per far passare il test: ricatturarla. */
const REAL_RESPONSE = {
  models: [
    {
      id: '4bb618e8-1287-4306-a540-b19176d64ff8',
      modelId: 'meta-llama/llama-3.3-70b-instruct:free',
      displayName: 'Llama 3.3 70B Instruct (Free)',
      provider: 'OpenRouter',
      priority: 1,
      isActive: true,
      isPrimary: true,
      createdAt: '2026-07-13T06:48:23.540528Z',
      updatedAt: null,
      settings: {
        maxTokens: 4096,
        temperature: 0.7,
        pricing: {
          inputPricePerMillion: 0,
          outputPricePerMillion: 0,
          currency: 'USD',
        },
      },
      usage: {
        totalRequests: 0,
        totalInputTokens: 0,
        totalOutputTokens: 0,
        totalTokensUsed: 0,
        totalCostUsd: 0,
        lastUsedAt: null,
      },
    },
  ],
  totalCount: 6,
  page: 1,
  pageSize: 20,
};

describe('PagedAiModelsSchema', () => {
  it('accetta la risposta reale del backend', () => {
    const result = PagedAiModelsSchema.safeParse(REAL_RESPONSE);

    // In caso di fallimento, stampa il motivo: un `expect(ok).toBe(true)` nudo costringerebbe
    // chi legge a rieseguire il test per sapere quale campo non combacia.
    expect(result.success ? null : result.error.issues).toBeNull();
  });

  it('rifiuta la forma che lo schema dichiarava prima del #4093', () => {
    // Il controllo al rovescio: se qualcuno ripristinasse `items`/`total`, il test sopra
    // tornerebbe rosso — ma solo questo dimostra che lo schema non accetta ENTRAMBE le forme.
    const oldShape = { items: REAL_RESPONSE.models, total: 6, page: 1, pageSize: 20 };

    expect(PagedAiModelsSchema.safeParse(oldShape).success).toBe(false);
  });

  it('accetta un provider che non è fra quelli noti', () => {
    // #4093: `provider` è una stringa libera lato backend. Un enum chiuso faceva fallire la
    // lettura dell'INTERA lista al primo provider nuovo, non solo della riga interessata.
    const withNewProvider = {
      ...REAL_RESPONSE.models[0],
      provider: 'UnProviderCheNonEsistevaIeri',
    };

    expect(AiModelDtoSchema.safeParse(withNewProvider).success).toBe(true);
  });

  it('rifiuta un elemento a cui manca `settings`', () => {
    const { settings: _settings, ...withoutSettings } = REAL_RESPONSE.models[0];

    expect(AiModelDtoSchema.safeParse(withoutSettings).success).toBe(false);
  });
});
