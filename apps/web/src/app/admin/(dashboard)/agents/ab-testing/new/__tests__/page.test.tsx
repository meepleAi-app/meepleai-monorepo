import { vi, describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
  usePathname: () => '/admin/agents/ab-testing/new',
}));

vi.mock('../../../NavConfig', () => ({
  AgentsNavConfig: () => null,
}));

vi.mock('@/hooks/useAdminConfig', () => ({
  useAdminConfig: () => ({ data: undefined, isLoading: false }),
  parseConfigValue: () => null,
}));

const mockCreateAbTest = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      createAbTest: (...args: unknown[]) => mockCreateAbTest(...args),
    },
  },
}));

// ---------------------------------------------------------------------------
// Component under test
// ---------------------------------------------------------------------------

import NewAbTestPage from '../page';

// The 8 model display names exactly as they appear in the component.
const MODEL_NAMES = [
  'GPT-4o',
  'GPT-4o Mini',
  'Claude 3.5 Sonnet',
  'Claude 3 Haiku',
  'Gemini 2.0 Flash',
  'Llama 3.1 70B',
  'Mistral Large',
  'Qwen 2.5 72B',
];

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Click the label row for a given model name to toggle its checkbox. */
function toggleModel(name: string) {
  // Each model row is a <label> containing the model name as a <span>.
  // Clicking anywhere on that label triggers the Radix Checkbox.
  const span = screen.getByText(name);
  // Walk up to the wrapping <label> and click it.
  const label = span.closest('label');
  expect(label).toBeTruthy();
  fireEvent.click(label!);
}

/** Fill the query textarea with the given text. */
function fillQuery(text: string) {
  const textarea = screen.getByPlaceholderText(/what are the rules for setting up/i);
  fireEvent.change(textarea, { target: { value: text } });
}

/** Return the "Generate Comparison" / "Generating Responses..." button. */
function getGenerateButton() {
  // During generation the label changes, so find by role.
  return screen.getByRole('button', {
    name: /generate comparison|generating responses/i,
  });
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('NewAbTestPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // 1. Renders the page heading
  it('renders the page title "New A/B Test"', () => {
    render(<NewAbTestPage />);

    expect(screen.getByRole('heading', { name: /new a\/b test/i })).toBeInTheDocument();
  });

  // 2. All 8 model checkboxes are present
  it('renders all 8 model checkboxes', () => {
    render(<NewAbTestPage />);

    for (const name of MODEL_NAMES) {
      expect(screen.getByText(name)).toBeInTheDocument();
    }

    // Exactly 8 checkbox roles
    const checkboxes = screen.getAllByRole('checkbox');
    expect(checkboxes).toHaveLength(8);
  });

  // 3. Generate button is disabled when no models and no query
  it('disables the Generate button when no models are selected or no query is entered', () => {
    render(<NewAbTestPage />);

    const btn = getGenerateButton();
    expect(btn).toBeDisabled();
  });

  // 4. Selecting models updates their checked state
  it('can select models and they show a checked state', () => {
    render(<NewAbTestPage />);

    const checkboxes = screen.getAllByRole('checkbox');
    // All start unchecked
    checkboxes.forEach(cb => {
      expect(cb).toHaveAttribute('data-state', 'unchecked');
    });

    toggleModel('GPT-4o');
    toggleModel('Mistral Large');

    // The two toggled checkboxes should now be checked
    expect(checkboxes[0]).toHaveAttribute('data-state', 'checked');
    expect(checkboxes[6]).toHaveAttribute('data-state', 'checked');
  });

  // 5. Cannot select more than MAX_MODELS (4)
  it('cannot select more than 4 models', () => {
    render(<NewAbTestPage />);

    // Select 4 models
    toggleModel('GPT-4o');
    toggleModel('GPT-4o Mini');
    toggleModel('Claude 3.5 Sonnet');
    toggleModel('Claude 3 Haiku');

    // Attempt to select a 5th
    toggleModel('Gemini 2.0 Flash');

    const checkboxes = screen.getAllByRole('checkbox');
    // Gemini (index 4) should remain unchecked
    expect(checkboxes[4]).toHaveAttribute('data-state', 'unchecked');

    // Only 4 are checked
    const checked = checkboxes.filter(cb => cb.getAttribute('data-state') === 'checked');
    expect(checked).toHaveLength(4);
  });

  // 6. Button enabled when query + 2 models
  it('enables the Generate button when a query is entered and at least 2 models are selected', () => {
    render(<NewAbTestPage />);

    fillQuery('Explain the rules of Catan');
    toggleModel('GPT-4o');

    // Still disabled with only 1 model
    expect(getGenerateButton()).toBeDisabled();

    toggleModel('Claude 3.5 Sonnet');

    // Now enabled
    expect(getGenerateButton()).toBeEnabled();
  });

  // 7. Successful submission calls createAbTest and redirects
  it('calls createAbTest on submit and redirects to the result page', async () => {
    mockCreateAbTest.mockResolvedValueOnce({ id: 'abc-123' });

    render(<NewAbTestPage />);

    fillQuery('Compare model creativity');
    toggleModel('GPT-4o');
    toggleModel('Claude 3.5 Sonnet');

    fireEvent.click(getGenerateButton());

    await waitFor(() => {
      expect(mockCreateAbTest).toHaveBeenCalledTimes(1);
      expect(mockCreateAbTest).toHaveBeenCalledWith({
        query: 'Compare model creativity',
        modelIds: ['openai/gpt-4o', 'anthropic/claude-3.5-sonnet'],
      });
    });

    await waitFor(() => {
      expect(mockPush).toHaveBeenCalledWith('/admin/agents/ab-testing/abc-123');
    });
  });

  // 8. Failed submission shows error alert
  it('shows an error alert when createAbTest fails', async () => {
    mockCreateAbTest.mockRejectedValueOnce(new Error('Service unavailable'));

    render(<NewAbTestPage />);

    fillQuery('Test query');
    toggleModel('GPT-4o');
    toggleModel('Claude 3 Haiku');

    fireEvent.click(getGenerateButton());

    await waitFor(() => {
      expect(screen.getByText('Service unavailable')).toBeInTheDocument();
    });

    // Should NOT have redirected
    expect(mockPush).not.toHaveBeenCalled();
  });
});
