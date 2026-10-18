import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { AiModelDto } from '@/lib/api/admin-ai-models';

import { AgentBuilderForm } from '../AgentBuilderForm';

/**
 * Issue #4102 / #4138 — the model dropdown must not offer an id the backend
 * rejects as unroutable.
 *
 * `llm-model-routing.test.ts` already covers the predicate, and that is exactly
 * why it is not enough: a correct predicate and a form that never calls it
 * produce the same green. If this dropdown went back to a hardcoded array, every
 * assertion in that file would still pass. These tests assert the *form*.
 *
 * Both directions on purpose. A filter that dropped everything would satisfy
 * "does not offer gpt-4" while making the form unusable, so the routable model
 * must also be present.
 */

const useAdminAiModels = vi.fn();
vi.mock('@/hooks/queries/useAdminAiModels', () => ({
  useAdminAiModels: () => useAdminAiModels(),
}));

function model(overrides: Partial<AiModelDto> & Pick<AiModelDto, 'modelId'>): AiModelDto {
  return {
    id: `id-${overrides.modelId}`,
    displayName: overrides.modelId,
    provider: 'test',
    priority: 1,
    isActive: true,
    isPrimary: false,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: null,
    settings: {} as AiModelDto['settings'],
    usage: {} as AiModelDto['usage'],
    ...overrides,
  };
}

function mockModels(data: AiModelDto[], state: { isLoading?: boolean; isError?: boolean } = {}) {
  useAdminAiModels.mockReturnValue({
    data,
    isLoading: state.isLoading ?? false,
    isError: state.isError ?? false,
  });
}

/** The form has several Selects (chat language first); anchor on the label. */
function modelTrigger(): HTMLElement {
  return screen.getByRole('combobox', { name: /^Model$/i });
}

/** Opens the model Select and returns the labels it offers. */
async function readModelOptions(): Promise<string[]> {
  const user = userEvent.setup();
  await user.click(modelTrigger());
  return screen.getAllByRole('option').map(o => o.textContent ?? '');
}

describe('AgentBuilderForm — the model dropdown (#4102)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('does not offer an unroutable bare cloud id the API returns', async () => {
    mockModels([model({ modelId: 'gpt-4' }), model({ modelId: 'deepseek-chat' })]);
    render(<AgentBuilderForm onSubmit={vi.fn()} />);

    const options = await readModelOptions();

    expect(options).toContain('deepseek-chat');
    expect(options).not.toContain('gpt-4');
  });

  // The four ids the hardcoded array used to offer. Named individually rather
  // than counted, so a future prefix change names the one that slipped through.
  it.each(['gpt-4', 'gpt-4-turbo', 'claude-3-opus', 'claude-3-sonnet'])(
    'does not offer %s',
    async modelId => {
      mockModels([model({ modelId }), model({ modelId: 'anthropic/claude-3.5-haiku' })]);
      render(<AgentBuilderForm onSubmit={vi.fn()} />);

      const options = await readModelOptions();

      expect(options).toContain('anthropic/claude-3.5-haiku');
      expect(options).not.toContain(modelId);
    }
  );

  it('does not offer a routable model that is deactivated', async () => {
    mockModels([
      model({ modelId: 'deepseek-chat', isActive: false }),
      model({ modelId: 'deepseek-reasoner' }),
    ]);
    render(<AgentBuilderForm onSubmit={vi.fn()} />);

    const options = await readModelOptions();

    expect(options).toContain('deepseek-reasoner');
    expect(options).not.toContain('deepseek-chat');
  });

  // Saying "nothing is available" beats an empty dropdown: the admin needs to
  // know the fix is in /admin/ai-models, not in this form.
  it('explains itself when nothing active and routable is available', () => {
    mockModels([model({ modelId: 'gpt-4' }), model({ modelId: 'claude-3-opus' })]);
    render(<AgentBuilderForm onSubmit={vi.fn()} />);

    expect(screen.getByText(/No active, routable model is available/i)).toBeInTheDocument();
  });

  it('distinguishes a failed model list from an empty one', () => {
    mockModels([], { isError: true });
    render(<AgentBuilderForm onSubmit={vi.fn()} />);

    expect(screen.getByText(/Could not load the model list/i)).toBeInTheDocument();
  });

  // There is no safe hardcoded default: the routable set is known only once
  // /admin/ai-models answers. A default of 'gpt-4' is what made the form
  // unsubmittable for anyone who did not touch the field.
  it('starts with no model selected rather than a guessed default', () => {
    mockModels([model({ modelId: 'deepseek-chat' })]);
    render(<AgentBuilderForm onSubmit={vi.fn()} />);

    expect(modelTrigger()).toHaveTextContent(/Select a model/i);
  });
});
