import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  AdminPromptTemplatesApiError,
  type PromptTemplateDto,
} from '@/lib/api/admin-prompt-templates';

const mockLoggerDebug = vi.hoisted(() => vi.fn());
vi.mock('@/lib/logger', () => ({
  logger: {
    debug: (...args: unknown[]) => mockLoggerDebug(...args),
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
  },
  getLogger: () => ({
    debug: (...args: unknown[]) => mockLoggerDebug(...args),
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
  }),
  resetLogger: vi.fn(),
  LogLevel: { DEBUG: 'debug', INFO: 'info', WARN: 'warn', ERROR: 'error' },
}));

const seedPrompts: PromptTemplateDto[] = [
  {
    id: 'tpl-rules-expert',
    name: 'Rules Expert',
    description: 'Specialized in explaining game rules and resolving rule disputes',
    category: 'qa',
    createdByUserId: 'usr-admin',
    createdByEmail: 'admin@example.com',
    createdAt: '2026-02-15T10:00:00Z',
    versionCount: 3,
    activeVersionNumber: 3,
  },
  {
    id: 'tpl-strategy',
    name: 'Strategy Advisor',
    description: null,
    category: null,
    createdByUserId: 'usr-admin',
    createdByEmail: null,
    createdAt: '2026-02-12T09:00:00Z',
    versionCount: 1,
    activeVersionNumber: 1,
  },
];

const mocks = vi.hoisted(() => ({
  queryState: {
    data: undefined as PromptTemplateDto[] | undefined,
    isLoading: false,
    isError: false,
    error: null as unknown,
  },
}));

vi.mock('@/hooks/queries/useAdminPromptTemplates', () => ({
  useAdminPromptTemplates: () => ({
    data: mocks.queryState.data,
    isLoading: mocks.queryState.isLoading,
    isError: mocks.queryState.isError,
    error: mocks.queryState.error,
  }),
}));

import { SystemPromptsSection } from '../system-prompts-section';

beforeEach(() => {
  mocks.queryState.data = seedPrompts;
  mocks.queryState.isLoading = false;
  mocks.queryState.isError = false;
  mocks.queryState.error = null;
  mockLoggerDebug.mockReset();
});

describe('SystemPromptsSection (#1442 Phase 1b — live API)', () => {
  it('renders the section heading', () => {
    render(<SystemPromptsSection />);
    expect(screen.getByText('System Prompts')).toBeInTheDocument();
  });

  it('renders a card per prompt template from the server', () => {
    render(<SystemPromptsSection />);
    expect(screen.getByText('Rules Expert')).toBeInTheDocument();
    expect(screen.getByText('Strategy Advisor')).toBeInTheDocument();
  });

  it('renders description text, falling back to em-dash when null', () => {
    render(<SystemPromptsSection />);
    expect(screen.getByText(/specialized in explaining game rules/i)).toBeInTheDocument();
    // Strategy Advisor has a null description — em-dash appears in its card.
    const strategyCard = screen.getByText('Strategy Advisor').closest('div')
      ?.parentElement?.parentElement;
    expect(strategyCard).not.toBeNull();
    expect(strategyCard!.textContent).toContain('—');
  });

  it('renders pluralized version count badge', () => {
    render(<SystemPromptsSection />);
    expect(screen.getByText('3 versions')).toBeInTheDocument();
    expect(screen.getByText('1 version')).toBeInTheDocument();
  });

  it('renders an ISO date for `Created`', () => {
    render(<SystemPromptsSection />);
    expect(screen.getByText(/created 2026-02-15/i)).toBeInTheDocument();
  });

  it('renders loading state when the query is loading', () => {
    mocks.queryState.data = undefined;
    mocks.queryState.isLoading = true;
    render(<SystemPromptsSection />);
    expect(screen.getByText(/loading system prompts/i)).toBeInTheDocument();
  });

  it('renders an error banner when the query fails', () => {
    mocks.queryState.data = undefined;
    mocks.queryState.isError = true;
    mocks.queryState.error = new AdminPromptTemplatesApiError(500, 'database unavailable');
    render(<SystemPromptsSection />);
    expect(screen.getByRole('alert')).toHaveTextContent(
      /failed to load prompts.*database unavailable/i
    );
  });

  it('renders an empty state when no prompts exist', () => {
    mocks.queryState.data = [];
    render(<SystemPromptsSection />);
    expect(screen.getByText(/no system prompts configured/i)).toBeInTheDocument();
  });

  it('logs a debug stub when the View button is clicked (Phase 1b deferred)', () => {
    render(<SystemPromptsSection />);
    fireEvent.click(screen.getByLabelText(/view prompt rules expert/i));
    expect(mockLoggerDebug).toHaveBeenCalledWith(
      expect.stringContaining('View prompt template tpl-rules-expert')
    );
  });

  it('logs a debug stub when the Edit button is clicked (Phase 1b deferred)', () => {
    render(<SystemPromptsSection />);
    fireEvent.click(screen.getByLabelText(/edit prompt rules expert/i));
    expect(mockLoggerDebug).toHaveBeenCalledWith(
      expect.stringContaining('Edit prompt template tpl-rules-expert')
    );
  });
});
