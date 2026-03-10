import { vi, describe, it, expect, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { StrategyEditor } from '../StrategyEditor';
import type { CreateStrategy } from '@/lib/api/schemas/strategies.schemas';

const mockOnSubmit = vi.fn();

function renderEditor(props: Partial<React.ComponentProps<typeof StrategyEditor>> = {}) {
  return render(<StrategyEditor onSubmit={mockOnSubmit} {...props} />);
}

describe('StrategyEditor', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders form with name, description fields and template buttons', () => {
    renderEditor();

    expect(screen.getByLabelText('Strategy Name')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('My Custom Strategy')).toBeInTheDocument();
    expect(screen.getByLabelText('Description')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Describe this strategy...')).toBeInTheDocument();
    expect(screen.getByText('Quick Templates:')).toBeInTheDocument();
  });

  it('shows empty state message when no steps are present', () => {
    renderEditor();

    expect(
      screen.getByText(/No steps added\. Click "Add Step" or load a template\./)
    ).toBeInTheDocument();
  });

  it('shows Add Step button', () => {
    renderEditor();

    expect(screen.getByRole('button', { name: /Add Step/i })).toBeInTheDocument();
  });

  it('renders template buttons for FAST, BALANCED, and PRECISE', () => {
    renderEditor();

    expect(screen.getByRole('button', { name: 'FAST' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'BALANCED' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'PRECISE' })).toBeInTheDocument();
  });

  it('fills form fields when a template button is clicked', async () => {
    const user = userEvent.setup();
    renderEditor();

    const nameInput = screen.getByPlaceholderText('My Custom Strategy') as HTMLInputElement;
    const descInput = screen.getByPlaceholderText(
      'Describe this strategy...'
    ) as HTMLTextAreaElement;

    expect(nameInput.value).toBe('');

    await user.click(screen.getByRole('button', { name: 'FAST' }));

    await waitFor(() => {
      expect(nameInput.value).toBe('Fast Retrieval');
    });
    expect(descInput.value).toBe('Quick vector search with single generation step');
    // Template loaded steps, so empty state should disappear
    expect(screen.queryByText(/No steps added/)).not.toBeInTheDocument();
  });

  it('shows "Save Strategy" button and "Saving..." when isLoading is true', () => {
    const { rerender } = render(<StrategyEditor onSubmit={mockOnSubmit} isLoading={false} />);

    const submitBtn = screen.getByRole('button', { name: 'Save Strategy' });
    expect(submitBtn).toBeInTheDocument();
    expect(submitBtn).not.toBeDisabled();

    rerender(<StrategyEditor onSubmit={mockOnSubmit} isLoading={true} />);

    const savingBtn = screen.getByRole('button', { name: 'Saving...' });
    expect(savingBtn).toBeInTheDocument();
    expect(savingBtn).toBeDisabled();
  });

  it('renders with defaultValues when provided', () => {
    const defaults: Partial<CreateStrategy> = {
      name: 'Pre-filled Strategy',
      description: 'Already has a description',
      steps: [
        { type: 'retrieval', config: { topK: 3 }, order: 0 },
        { type: 'generation', config: {}, order: 1 },
      ],
    };

    renderEditor({ defaultValues: defaults });

    const nameInput = screen.getByPlaceholderText('My Custom Strategy') as HTMLInputElement;
    const descInput = screen.getByPlaceholderText(
      'Describe this strategy...'
    ) as HTMLTextAreaElement;

    expect(nameInput.value).toBe('Pre-filled Strategy');
    expect(descInput.value).toBe('Already has a description');
    // Steps are rendered, so empty state should not show
    expect(screen.queryByText(/No steps added/)).not.toBeInTheDocument();
    // Pipeline Preview should be visible with steps
    expect(screen.getByText('Pipeline Preview')).toBeInTheDocument();
  });

  it('shows Pipeline Steps heading', () => {
    renderEditor();

    expect(screen.getByText('Pipeline Steps')).toBeInTheDocument();
  });
});
