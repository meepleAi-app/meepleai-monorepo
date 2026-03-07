import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

// Mock all tab components
vi.mock('../AgentsTab', () => ({ AgentsTab: () => <div data-testid="agents-tab" /> }));
vi.mock('../TypologiesTab', () => ({
  TypologiesTab: () => <div data-testid="typologies-tab" />,
}));
vi.mock('../DefinitionsTab', () => ({
  DefinitionsTab: () => <div data-testid="definitions-tab" />,
}));
vi.mock('../AiLabTab', () => ({ AiLabTab: () => <div data-testid="lab-tab" /> }));
vi.mock('../PromptsTab', () => ({ PromptsTab: () => <div data-testid="prompts-tab" /> }));
vi.mock('../ModelsTab', () => ({ ModelsTab: () => <div data-testid="models-tab" /> }));
vi.mock('../RequestsTab', () => ({ RequestsTab: () => <div data-testid="requests-tab" /> }));
vi.mock('../RagTab', () => ({ RagTab: () => <div data-testid="rag-tab" /> }));
vi.mock('../NavConfig', () => ({ AdminAiNavConfig: () => null }));

import AdminAiPage from '../page';

describe('AdminAiPage', () => {
  it('renders agents tab by default', async () => {
    const page = await AdminAiPage({ searchParams: Promise.resolve({}) });
    render(page);
    expect(screen.getByTestId('agents-tab')).toBeInTheDocument();
  });

  it('renders typologies tab when tab=typologies', async () => {
    const page = await AdminAiPage({
      searchParams: Promise.resolve({ tab: 'typologies' }),
    });
    render(page);
    expect(screen.getByTestId('typologies-tab')).toBeInTheDocument();
  });

  it('renders definitions tab when tab=definitions', async () => {
    const page = await AdminAiPage({
      searchParams: Promise.resolve({ tab: 'definitions' }),
    });
    render(page);
    expect(screen.getByTestId('definitions-tab')).toBeInTheDocument();
  });

  it('renders lab tab when tab=lab', async () => {
    const page = await AdminAiPage({
      searchParams: Promise.resolve({ tab: 'lab' }),
    });
    render(page);
    expect(screen.getByTestId('lab-tab')).toBeInTheDocument();
  });

  it('renders prompts tab when tab=prompts', async () => {
    const page = await AdminAiPage({
      searchParams: Promise.resolve({ tab: 'prompts' }),
    });
    render(page);
    expect(screen.getByTestId('prompts-tab')).toBeInTheDocument();
  });

  it('renders models tab when tab=models', async () => {
    const page = await AdminAiPage({
      searchParams: Promise.resolve({ tab: 'models' }),
    });
    render(page);
    expect(screen.getByTestId('models-tab')).toBeInTheDocument();
  });

  it('renders requests tab when tab=requests', async () => {
    const page = await AdminAiPage({
      searchParams: Promise.resolve({ tab: 'requests' }),
    });
    render(page);
    expect(screen.getByTestId('requests-tab')).toBeInTheDocument();
  });

  it('renders rag tab when tab=rag', async () => {
    const page = await AdminAiPage({
      searchParams: Promise.resolve({ tab: 'rag' }),
    });
    render(page);
    expect(screen.getByTestId('rag-tab')).toBeInTheDocument();
  });

  it('renders page title', async () => {
    const page = await AdminAiPage({ searchParams: Promise.resolve({}) });
    render(page);
    expect(screen.getByText('AI & Agents')).toBeInTheDocument();
  });

  it('renders all tab links', async () => {
    const page = await AdminAiPage({ searchParams: Promise.resolve({}) });
    render(page);
    expect(screen.getByText('Agents')).toBeInTheDocument();
    expect(screen.getByText('Typologies')).toBeInTheDocument();
    expect(screen.getByText('Definitions')).toBeInTheDocument();
    expect(screen.getByText('AI Lab')).toBeInTheDocument();
    expect(screen.getByText('Prompts')).toBeInTheDocument();
    expect(screen.getByText('Models')).toBeInTheDocument();
    expect(screen.getByText('Requests')).toBeInTheDocument();
    expect(screen.getByText('RAG')).toBeInTheDocument();
  });

  it('highlights active tab', async () => {
    const page = await AdminAiPage({
      searchParams: Promise.resolve({ tab: 'models' }),
    });
    render(page);
    const modelsLink = screen.getByText('Models').closest('a');
    expect(modelsLink?.className).toContain('bg-primary');
  });
});
