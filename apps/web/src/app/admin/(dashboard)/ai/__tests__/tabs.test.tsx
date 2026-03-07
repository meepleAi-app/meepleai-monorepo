import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getStats: vi.fn().mockResolvedValue({ agents: [] }),
  getAgentTypologies: vi.fn().mockResolvedValue([]),
  getPrompts: vi.fn().mockResolvedValue([]),
  getAiModels: vi.fn().mockResolvedValue({ items: [] }),
  getAiRequests: vi.fn().mockResolvedValue({ items: [] }),
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
