import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getStats: vi.fn().mockResolvedValue({ agents: [] }),
  getAgentTypologies: vi.fn().mockResolvedValue([]),
  getPrompts: vi.fn().mockResolvedValue([]),
  getAiModels: vi.fn().mockResolvedValue({ models: [], totalCount: 0, page: 1, pageSize: 20 }),
  getAiRequests: vi.fn().mockResolvedValue({ requests: [], totalCount: 0 }),
  getModelPerformance: vi.fn().mockResolvedValue({
    totalRequests: 0,
    totalCost: 0,
    totalTokens: 0,
    avgLatencyMs: 0,
    successRate: 0,
    models: [],
    dailyStats: [],
  }),
  // #1729: RequestsTab now drives the trend chart via getAiMetricsTrend
  getAiMetricsTrend: vi.fn().mockResolvedValue({
    range: '7d',
    bucketSize: '1h',
    datapoints: [],
  }),
  deletePrompt: vi.fn().mockResolvedValue(undefined),
  approveAgentTypology: vi.fn().mockResolvedValue(undefined),
  deleteAgentTypology: vi.fn().mockResolvedValue(undefined),
  setPrimaryModel: vi.fn().mockResolvedValue(undefined),
}));

vi.mock('@/lib/api', () => ({
  api: {
    admin: mocks,
  },
}));

// RequestsTab uses next/navigation router/searchParams to drive the
// QueryDrillPanel deep-link (#1722 PR 2/4).
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    replace: vi.fn(),
    push: vi.fn(),
  }),
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/components/ui/primitives/button', () => ({
  Button: ({
    children,
    ...props
  }: React.ButtonHTMLAttributes<HTMLButtonElement> & {
    children?: React.ReactNode;
    variant?: string;
    size?: string;
  }) => <button {...props}>{children}</button>,
}));

import { AgentsTab } from '../AgentsTab';
import { AiLabTab } from '../AiLabTab';
import { DefinitionsTab } from '../DefinitionsTab';
import { ModelsTab } from '../ModelsTab';
import { PromptsTab } from '../PromptsTab';
import { RagTab } from '../RagTab';
import { RequestsTab } from '../RequestsTab';
import { TypologiesTab } from '../TypologiesTab';

describe('AgentsTab', () => {
  it('renders heading', () => {
    render(<AgentsTab />);
    expect(screen.getByText('Agent Catalog')).toBeInTheDocument();
  });

  it('shows empty state when no agents', async () => {
    render(<AgentsTab />);
    expect(await screen.findByText('No agents found')).toBeInTheDocument();
  });

  it('renders link to full catalog', () => {
    render(<AgentsTab />);
    expect(screen.getByText('Full Catalog')).toBeInTheDocument();
  });
});

describe('TypologiesTab', () => {
  it('renders heading', () => {
    render(<TypologiesTab />);
    expect(screen.getByText('Agent Typologies')).toBeInTheDocument();
  });

  it('shows empty state when no typologies', async () => {
    render(<TypologiesTab />);
    expect(await screen.findByText('No typologies found')).toBeInTheDocument();
  });
});

describe('DefinitionsTab', () => {
  it('renders heading', () => {
    render(<DefinitionsTab />);
    expect(screen.getByText('Agent Definitions')).toBeInTheDocument();
  });

  it('renders quick links', () => {
    render(<DefinitionsTab />);
    expect(screen.getByText('All Definitions')).toBeInTheDocument();
    expect(screen.getByText('Create New')).toBeInTheDocument();
    expect(screen.getByText('Agent Builder')).toBeInTheDocument();
  });
});

describe('AiLabTab', () => {
  it('renders heading', () => {
    render(<AiLabTab />);
    expect(screen.getByText('AI Lab')).toBeInTheDocument();
  });

  it('renders lab links', () => {
    render(<AiLabTab />);
    expect(screen.getByText('Agent Playground')).toBeInTheDocument();
    expect(screen.getByText('Debug Chat')).toBeInTheDocument();
    expect(screen.getByText('Debug Console')).toBeInTheDocument();
    expect(screen.getByText('Pipeline Explorer')).toBeInTheDocument();
  });
});

describe('PromptsTab', () => {
  it('renders heading', () => {
    render(<PromptsTab />);
    expect(screen.getByText('Prompt Templates')).toBeInTheDocument();
  });

  it('shows empty state when no prompts', async () => {
    render(<PromptsTab />);
    expect(await screen.findByText('No prompt templates found')).toBeInTheDocument();
  });
});

describe('ModelsTab', () => {
  it('renders heading', () => {
    render(<ModelsTab />);
    expect(screen.getByText('AI Models')).toBeInTheDocument();
  });

  it('shows empty state when no models', async () => {
    render(<ModelsTab />);
    expect(await screen.findByText('No AI models configured')).toBeInTheDocument();
  });

  // 🔴 #4059 — un errore di caricamento deve essere DISTINGUIBILE da «non ci sono modelli».
  //
  // Il difetto: `.catch(() => {})` cancellava l'errore e il tab mostrava lo stesso schermo
  // dello stato vuoto. Il backend mandava `models`/`totalCount` dove lo schema del client
  // attende `items`/`total`, quindi la validazione falliva — mentre la rotta rispondeva 200
  // con 6 modelli. Misurato in Chromium su quattro pagine admin: errore di schema in console
  // su tutte e quattro, nessun errore a schermo su nessuna.
  //
  // L'asserzione che conta e' la SECONDA: senza di essa il test passerebbe anche se il ramo
  // d'errore venisse reso oltre lo stato vuoto, cioe' senza distinguere i due casi.
  it('mostra un errore a schermo quando il caricamento fallisce, e NON lo stato vuoto', async () => {
    mocks.getAiModels.mockRejectedValueOnce(
      new Error('Schema validation failed: Response validation failed for /api/v1/admin/ai-models')
    );

    render(<ModelsTab />);

    const alert = await screen.findByTestId('models-tab-error');
    expect(alert).toHaveTextContent(/Impossibile caricare i modelli AI/);
    expect(alert).toHaveTextContent(/Schema validation failed/);
    expect(screen.queryByText('No AI models configured')).not.toBeInTheDocument();
  });

  it('legge `models`, cioe` la forma che il backend manda davvero', async () => {
    // Il controllo al rovescio del test sopra: con una risposta VALIDA il tab deve mostrare i
    // modelli, non l'errore. Senza questo, un componente che rendesse sempre il ramo d'errore
    // passerebbe il test precedente.
    //
    // #4093 ha sciolto la decisione che #4059 voce 4 aveva lasciata aperta: il contratto si
    // allinea al BACKEND, che e` quello in esecuzione — `models`/`totalCount` e i campi
    // `modelId`/`isActive`/`settings`/`usage`. Il fixture qui sotto e` copiato dalla risposta
    // reale di `GET /api/v1/admin/ai-models?status=active`, non derivato dallo schema che
    // dovrebbe verificare.
    mocks.getAiModels.mockResolvedValueOnce({
      models: [
        {
          id: '4bb618e8-1287-4306-a540-b19176d64ff8',
          modelId: 'meta-llama/llama-3.3-70b-instruct:free',
          displayName: 'gpt-test',
          provider: 'OpenRouter',
          priority: 1,
          isActive: true,
          isPrimary: false,
          createdAt: '2026-07-13T06:48:23.540528Z',
          updatedAt: null,
          settings: {
            maxTokens: 4096,
            temperature: 0.7,
            pricing: { inputPricePerMillion: 0, outputPricePerMillion: 0, currency: 'USD' },
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
      totalCount: 1,
      page: 1,
      pageSize: 20,
    });

    render(<ModelsTab />);

    expect(await screen.findByText('gpt-test')).toBeInTheDocument();
    // L'identificatore del modello e` reso accanto al provider: se il componente tornasse a
    // leggere `modelIdentifier` (inesistente) qui comparirebbe "undefined".
    expect(await screen.findByText(/OpenRouter · meta-llama/)).toBeInTheDocument();
    expect(screen.queryByText('No AI models configured')).not.toBeInTheDocument();
    expect(screen.queryByTestId('models-tab-error')).not.toBeInTheDocument();
  });
});

describe('RequestsTab', () => {
  it('renders heading', () => {
    render(<RequestsTab />);
    expect(screen.getByText('AI Requests')).toBeInTheDocument();
  });

  it('shows empty state when no requests', async () => {
    render(<RequestsTab />);
    expect(await screen.findByText('No AI requests found')).toBeInTheDocument();
  });

  it('renders refresh button', () => {
    render(<RequestsTab />);
    expect(screen.getByText('Refresh')).toBeInTheDocument();
  });
});

describe('RagTab', () => {
  it('renders heading', () => {
    render(<RagTab />);
    expect(screen.getByText('RAG Pipeline')).toBeInTheDocument();
  });

  it('renders all RAG links', () => {
    render(<RagTab />);
    expect(screen.getByText('Pipeline Explorer')).toBeInTheDocument();
    expect(screen.getByText('Debug Console')).toBeInTheDocument();
    expect(screen.getByText('Strategy Config')).toBeInTheDocument();
    expect(screen.getByText('Knowledge Base')).toBeInTheDocument();
    expect(screen.getByText('Vector Collections')).toBeInTheDocument();
    expect(screen.getByText('Debug Chat')).toBeInTheDocument();
  });
});
