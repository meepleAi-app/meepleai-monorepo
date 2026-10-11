/**
 * #4169 (ADR-095 D5/D8, fetta 6b): la pagina «Modelli e prezzi».
 *
 * Del catalogo il runtime legge davvero il prezzo (costo, tetto e degrado del gateway), lo stato
 * attivo e il predefinito di ciascun provider (ordine di fallback). Prima di questa fetta la pagina
 * non mostrava né modificava il prezzo, «Set Primary» chiamava una rotta inesistente e «Configure»
 * non faceva nulla.
 */
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { ModelsTab } from '../ModelsTab';

const mockGetAiModels = vi.hoisted(() => vi.fn());
const mockUpdatePricing = vi.hoisted(() => vi.fn());
const mockSetPrimary = vi.hoisted(() => vi.fn());
const mockToggle = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAiModels: mockGetAiModels,
      updateModelPricing: mockUpdatePricing,
      setPrimaryModel: mockSetPrimary,
      toggleModelActive: mockToggle,
    },
  },
}));

const USAGE = {
  totalRequests: 0,
  totalInputTokens: 0,
  totalOutputTokens: 0,
  totalTokensUsed: 0,
  totalCostUsd: 0,
  lastUsedAt: null,
};

function model(
  id: string,
  modelId: string,
  provider: string,
  input: number,
  output: number,
  extra: Partial<{
    isActive: boolean;
    isPrimary: boolean;
    verifiedOn: string | null;
    source: string | null;
  }> = {}
) {
  return {
    id,
    modelId,
    displayName: modelId,
    provider,
    priority: 1,
    isActive: extra.isActive ?? true,
    isPrimary: extra.isPrimary ?? false,
    createdAt: '2026-10-10T00:00:00Z',
    updatedAt: null,
    settings: {
      maxTokens: 4096,
      temperature: 0.3,
      pricing: {
        inputPricePerMillion: input,
        outputPricePerMillion: output,
        currency: 'USD',
        cacheReadPricePerMillion: null,
        verifiedOn: extra.verifiedOn ?? '2026-10-10',
        source: extra.source ?? 'https://openrouter.ai/api/v1/models',
      },
    },
    usage: USAGE,
  };
}

const LLAMA = model(
  '11111111-1111-4111-8111-111111111111',
  'meta-llama/llama-3.3-70b-instruct',
  'OpenRouter',
  0.22,
  0.5,
  {
    isPrimary: true,
  }
);
const DEEPSEEK_OR = model(
  '22222222-2222-4222-8222-222222222222',
  'deepseek/deepseek-chat',
  'OpenRouter',
  0.32,
  0.89
);
const NO_PRICE = model(
  '33333333-3333-4333-8333-333333333333',
  'some/unpriced-model',
  'OpenRouter',
  0,
  0,
  {
    verifiedOn: null,
    source: null,
  }
);
const LOCAL = model('44444444-4444-4444-8444-444444444444', 'llama3:8b', 'Ollama', 0, 0, {
  isActive: false,
  verifiedOn: null,
  source: null,
});

function page(models: unknown[]) {
  return { models, totalCount: models.length, page: 1, pageSize: 100 };
}

// Il nome della riga è «<modelId> (<provider>)»: confronto esatto sul prefisso, senza costruire una
// regex dal testo (CodeQL js/incomplete-sanitization sull'escape fatto a mano).
const rowOf = (modelId: string) =>
  screen.getByRole('row', { name: name => name.startsWith(`${modelId} (`) });

describe('ModelsTab — Modelli e prezzi (#4169)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAiModels.mockResolvedValue(page([LLAMA, DEEPSEEK_OR, NO_PRICE, LOCAL]));
    mockUpdatePricing.mockResolvedValue(LLAMA);
    mockSetPrimary.mockResolvedValue(DEEPSEEK_OR);
    mockToggle.mockResolvedValue(LOCAL);
  });

  it('shows the price per million tokens and where it comes from', async () => {
    renderWithQuery(<ModelsTab />);

    const row = await screen.findByRole('row', { name: /llama-3\.3-70b-instruct/ });
    expect(row).toHaveTextContent('$0.22');
    expect(row).toHaveTextContent('$0.50');
    expect(row).toHaveTextContent('2026-10-10');
  });

  it('saves a new price for one model', async () => {
    const user = userEvent.setup();
    renderWithQuery(<ModelsTab />);
    await screen.findByRole('row', { name: /deepseek\/deepseek-chat/ });

    await user.click(
      within(rowOf('deepseek/deepseek-chat')).getByRole('button', { name: /edit price/i })
    );
    const input = screen.getByLabelText(/input price/i);
    await user.clear(input);
    await user.type(input, '0.3');
    const output = screen.getByLabelText(/output price/i);
    await user.clear(output);
    await user.type(output, '0.9');
    await user.click(screen.getByRole('button', { name: /save price/i }));

    await waitFor(() =>
      expect(mockUpdatePricing).toHaveBeenCalledWith(DEEPSEEK_OR.id, {
        inputPricePerMillion: 0.3,
        outputPricePerMillion: 0.9,
      })
    );
  });

  it('makes a model the default of its provider through the real route', async () => {
    const user = userEvent.setup();
    renderWithQuery(<ModelsTab />);
    await screen.findByRole('row', { name: /deepseek\/deepseek-chat/ });

    await user.click(
      within(rowOf('deepseek/deepseek-chat')).getByRole('button', { name: /make default/i })
    );

    await waitFor(() => expect(mockSetPrimary).toHaveBeenCalledWith({ modelId: DEEPSEEK_OR.id }));
    await waitFor(() => expect(mockGetAiModels).toHaveBeenCalledTimes(2));
  });

  it('an inactive model cannot be made default, and the default cannot be deactivated', async () => {
    renderWithQuery(<ModelsTab />);
    await screen.findByRole('row', { name: /llama3:8b/ });

    expect(
      within(rowOf('llama3:8b')).getByRole('button', { name: /make default/i })
    ).toBeDisabled();
    expect(
      within(rowOf('meta-llama/llama-3.3-70b-instruct')).getByRole('switch', {
        name: /in fallback/i,
      })
    ).toBeDisabled();
  });

  it('toggles whether a model is in the fallback order', async () => {
    const user = userEvent.setup();
    renderWithQuery(<ModelsTab />);
    await screen.findByRole('row', { name: /llama3:8b/ });

    await user.click(within(rowOf('llama3:8b')).getByRole('switch', { name: /in fallback/i }));

    await waitFor(() => expect(mockToggle).toHaveBeenCalledWith(LOCAL.id));
  });

  it('flags a paid provider at 0/0 as unpriced, while a local model is free', async () => {
    renderWithQuery(<ModelsTab />);
    await screen.findByRole('row', { name: /unpriced-model/ });

    expect(rowOf('some/unpriced-model')).toHaveTextContent(/no price/i);
    expect(rowOf('llama3:8b')).toHaveTextContent(/free/i);
  });

  it('offers no setting the runtime does not read', async () => {
    renderWithQuery(<ModelsTab />);
    await screen.findByRole('row', { name: /llama3:8b/ });

    expect(screen.queryByRole('button', { name: /configure/i })).not.toBeInTheDocument();
  });
});
