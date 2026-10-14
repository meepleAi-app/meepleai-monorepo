/**
 * #4169 (ADR-095 D8, fetta 6c): la pagina «Agente». L'admin modifica la bozza delle sezioni che si
 * vedono leggendo una risposta (Identità, Modello, Generazione, Prompt), la confronta con la versione
 * pubblicata, la pubblica e torna indietro dalla storia. Recupero e Fonti si vedono ma non si
 * modificano fino alla fetta 7.
 */
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ApiError } from '@/lib/api/core/errors';

import { AgentProfileEditor } from '../AgentProfileEditor';

const mockGetProfile = vi.hoisted(() => vi.fn());
const mockSaveDraft = vi.hoisted(() => vi.fn());
const mockPublish = vi.hoisted(() => vi.fn());
const mockRollback = vi.hoisted(() => vi.fn());
const mockGetAiModels = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAgentProfile: mockGetProfile,
      saveAgentProfileDraft: mockSaveDraft,
      publishAgentProfileDraft: mockPublish,
      rollbackAgentProfile: mockRollback,
      getAiModels: mockGetAiModels,
    },
  },
}));

function content(overrides: Record<string, unknown> = {}) {
  return {
    name: 'MeepleAI',
    description: 'Board game rules assistant',
    persona: 'a precise rules assistant',
    primaryModelId: 'deepseek-flash',
    fallbackModelId: 'meta-llama/llama-3.3-70b-instruct',
    temperature: 0.3,
    maxResponseTokens: 1500,
    citationStyle: 'PageReferencesInText',
    systemPrompts: { en: 'You are MeepleAI.', it: 'Sei MeepleAI.' },
    fallbackLanguage: 'en',
    candidatePoolSize: 20,
    finalTopK: 5,
    minScore: 0.55,
    vectorWeight: 0.7,
    keywordWeight: 0.3,
    rerankerEnabled: true,
    allowedCategories: ['Rulebook', 'Errata'],
    sessionAssistant: {
      enabled: false,
      useReserveModel: false,
      promptLanguages: [],
      temperature: 0.2,
      maxResponseTokens: 600,
    },
    ...overrides,
  };
}

function version(versionNumber: number, status: string, c = content()) {
  return {
    id: `v${versionNumber}`,
    versionNumber,
    status,
    createdAt: '2026-10-10T10:00:00Z',
    publishedAt: status === 'Draft' ? null : '2026-10-10T10:00:00Z',
    archivedAt: status === 'Archived' ? '2026-10-12T10:00:00Z' : null,
    content: c,
  };
}

function profile(draft: ReturnType<typeof version> | null = null) {
  const published = version(2, 'Published');
  const summaries = [draft, published, version(1, 'Archived')]
    .filter((v): v is ReturnType<typeof version> => v !== null)
    .map(({ content: _c, ...rest }) => rest);
  return { published, draft, versions: summaries };
}

function catalogModel(modelId: string, provider: string, input: number, output: number) {
  return {
    id: `id-${modelId}`,
    modelId,
    displayName: modelId,
    provider,
    priority: 1,
    isActive: true,
    isPrimary: false,
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
        verifiedOn: null,
        source: null,
      },
    },
    usage: null,
  };
}

const CATALOG = [
  catalogModel('deepseek-flash', 'DeepSeek', 0.27, 1.1),
  catalogModel('meta-llama/llama-3.3-70b-instruct', 'OpenRouter', 0.1, 0.3),
  catalogModel('openrouter/zero-priced', 'OpenRouter', 0, 0),
];

beforeEach(() => {
  vi.clearAllMocks();
  mockGetProfile.mockResolvedValue(profile());
  mockGetAiModels.mockResolvedValue({
    models: CATALOG,
    totalCount: CATALOG.length,
    page: 1,
    pageSize: 100,
  });
});

async function renderLoaded() {
  render(<AgentProfileEditor />);
  await screen.findByLabelText('Name');
}

describe('AgentProfileEditor', () => {
  it('starts from the published version when there is no draft', async () => {
    await renderLoaded();

    expect(screen.getByText(/published: version 2/i)).toBeInTheDocument();
    expect(screen.getByText(/no draft/i)).toBeInTheDocument();
    expect(screen.getByLabelText('Name')).toHaveValue('MeepleAI');
    expect(screen.getByLabelText('Temperature')).toHaveValue(0.3);
    expect(screen.getByLabelText('System prompt (en)')).toHaveValue('You are MeepleAI.');
  });

  it('shows retrieval and sources read-only, and says why and until when', async () => {
    await renderLoaded();

    const retrieval = screen.getByRole('region', { name: /retrieval/i });
    expect(within(retrieval).getByText('0.55')).toBeInTheDocument();
    expect(within(retrieval).queryByRole('textbox')).not.toBeInTheDocument();
    expect(within(retrieval).queryByRole('spinbutton')).not.toBeInTheDocument();
    expect(screen.getByRole('region', { name: /sources/i })).toHaveTextContent('Rulebook');
    expect(screen.getAllByText(/read-only until/i).length).toBeGreaterThan(0);
  });

  it('saves only the editable sections', async () => {
    const user = userEvent.setup();
    mockSaveDraft.mockResolvedValue(profile(version(3, 'Draft', content({ temperature: 0.9 }))));
    await renderLoaded();

    const temperature = screen.getByLabelText('Temperature');
    await user.clear(temperature);
    await user.type(temperature, '0.9');
    await user.click(screen.getByRole('button', { name: /save draft/i }));

    await waitFor(() => expect(mockSaveDraft).toHaveBeenCalledTimes(1));
    const body = mockSaveDraft.mock.calls[0][0];
    expect(Object.keys(body).sort()).toEqual(
      [
        'citationStyle',
        'description',
        'fallbackLanguage',
        'fallbackModelId',
        'maxResponseTokens',
        'name',
        'persona',
        'primaryModelId',
        'systemPrompts',
        'temperature',
      ].sort()
    );
    expect(body.temperature).toBe(0.9);
  });

  it('publishes only a saved draft', async () => {
    await renderLoaded();

    expect(screen.getByRole('button', { name: /publish draft/i })).toBeDisabled();
  });

  it('publishes the saved draft', async () => {
    const user = userEvent.setup();
    mockGetProfile.mockResolvedValue(profile(version(3, 'Draft', content({ temperature: 0.9 }))));
    mockPublish.mockResolvedValue(profile());
    await renderLoaded();

    await user.click(screen.getByRole('button', { name: /publish draft/i }));

    await waitFor(() => expect(mockPublish).toHaveBeenCalledTimes(1));
  });

  it('compares the draft with the published version', async () => {
    mockGetProfile.mockResolvedValue(profile(version(3, 'Draft', content({ temperature: 0.9 }))));
    await renderLoaded();

    expect(screen.getByLabelText('Temperature')).toHaveValue(0.9);
    expect(screen.getByText('Published: 0.3')).toBeInTheDocument();
  });

  it('explains a refused publish', async () => {
    const user = userEvent.setup();
    mockGetProfile.mockResolvedValue(
      profile(version(3, 'Draft', content({ primaryModelId: 'test/no-price' })))
    );
    mockPublish.mockRejectedValue(
      new ApiError({
        message:
          "The model 'test/no-price' has no price in the model catalog: set its price before publishing.",
        statusCode: 409,
        code: 'model_without_price',
      })
    );
    await renderLoaded();

    await user.click(screen.getByRole('button', { name: /publish draft/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      /has no price in the model catalog/i
    );
  });

  it('warns about a model the catalog does not price', async () => {
    mockGetProfile.mockResolvedValue(
      profile(version(3, 'Draft', content({ primaryModelId: 'test/no-price' })))
    );
    await renderLoaded();

    expect(
      screen.getByText(/not in the model catalog: publishing is refused/i)
    ).toBeInTheDocument();
  });

  it('warns about a zero price on a paid provider, without blocking', async () => {
    mockGetProfile.mockResolvedValue(
      profile(version(3, 'Draft', content({ primaryModelId: 'openrouter/zero-priced' })))
    );
    await renderLoaded();

    expect(screen.getByText(/counted as free/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /publish draft/i })).toBeEnabled();
  });

  it('rolls back to an archived version after a confirmation', async () => {
    const user = userEvent.setup();
    mockRollback.mockResolvedValue(profile());
    await renderLoaded();

    const archived = screen.getByRole('row', { name: /version 1/i });
    await user.click(within(archived).getByRole('button', { name: /roll back/i }));
    expect(mockRollback).not.toHaveBeenCalled();
    await user.click(within(archived).getByRole('button', { name: /confirm/i }));

    await waitFor(() => expect(mockRollback).toHaveBeenCalledWith(1));
    expect(
      within(screen.getByRole('row', { name: /version 2/i })).queryByRole('button')
    ).not.toBeInTheDocument();
  });

  it('does not roll back over unsaved changes', async () => {
    // Il rollback rimette il modulo sulla bozza: le modifiche non salvate sparirebbero in silenzio.
    const user = userEvent.setup();
    await renderLoaded();

    const temperature = screen.getByLabelText('Temperature');
    await user.clear(temperature);
    await user.type(temperature, '0.9');

    const archived = screen.getByRole('row', { name: /version 1/i });
    expect(within(archived).getByRole('button', { name: /roll back/i })).toBeDisabled();
  });

  it('does not save an emptied number as zero', async () => {
    const user = userEvent.setup();
    await renderLoaded();

    await user.clear(screen.getByLabelText('Temperature'));

    expect(screen.getByRole('button', { name: /save draft/i })).toBeDisabled();
    expect(screen.getByText(/enter a number/i)).toBeInTheDocument();
  });

  it('names each roll back button after its version', async () => {
    await renderLoaded();

    expect(screen.getByRole('button', { name: 'Roll back to version 1' })).toBeInTheDocument();
  });

  it('says so when the profile cannot be loaded', async () => {
    mockGetProfile.mockRejectedValue(new Error('boom'));
    render(<AgentProfileEditor />);

    expect(await screen.findByRole('alert')).toHaveTextContent(/could not load the agent profile/i);
  });
});
