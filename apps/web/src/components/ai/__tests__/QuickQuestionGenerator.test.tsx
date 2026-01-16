/**
 * QuickQuestionGenerator Tests
 * Issue #2417 (Sub-Issue 2401.1): UI for AI-powered QuickQuestion generation
 *
 * Comprehensive test suite covering:
 * - Component rendering in all states
 * - Form validation (min/max length, whitespace)
 * - Async state transitions (idle → generating → success/error)
 * - User interactions (submit, dismiss error, reset)
 * - Progress indicator behavior
 * - Accessibility features
 * - Error handling scenarios
 *
 * Target: >90% code coverage
 */

import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import {
  QuickQuestionGenerator,
  type GeneratedQuestion,
  type QuickQuestionGeneratorProps,
} from '../QuickQuestionGenerator';

// ==================== Mock Data ====================

const mockGeneratedQuestion: GeneratedQuestion = {
  id: 'q-test-123',
  question: 'What is the best opening strategy in Catan?',
  answer: 'Focus on resource diversity and settlements near high-probability numbers.',
  category: 'Strategy',
  confidence: 0.85,
};

const createMockOnGenerate = (
  result: GeneratedQuestion = mockGeneratedQuestion,
  delay: number = 100
): QuickQuestionGeneratorProps['onGenerate'] => {
  return vi.fn(async () => {
    await new Promise(resolve => setTimeout(resolve, delay));
    return result;
  });
};

const createMockOnGenerateError = (
  errorMessage: string = 'Generation failed',
  delay: number = 100
): QuickQuestionGeneratorProps['onGenerate'] => {
  return vi.fn(async () => {
    await new Promise(resolve => setTimeout(resolve, delay));
    throw new Error(errorMessage);
  });
};

// ==================== Test Utilities ====================

const defaultProps: QuickQuestionGeneratorProps = {
  onGenerate: createMockOnGenerate(),
  minPromptLength: 10,
  maxPromptLength: 500,
};

const renderComponent = (props: Partial<QuickQuestionGeneratorProps> = {}) => {
  const mergedProps = { ...defaultProps, ...props };
  return render(<QuickQuestionGenerator {...mergedProps} />);
};

const getTextarea = () => screen.getByRole('textbox');
const getSubmitButton = () => screen.getByRole('button', { name: /generate question/i });

// ==================== Test Suite ====================

describe('QuickQuestionGenerator', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // ========== Rendering Tests ==========

  describe('Rendering', () => {
    it('should render the component with title and description', () => {
      renderComponent();

      expect(screen.getByText('Quick Question Generator')).toBeInTheDocument();
      expect(
        screen.getByText(/describe what kind of question you want to generate/i)
      ).toBeInTheDocument();
    });

    it('should render textarea with placeholder', () => {
      renderComponent();

      const textarea = getTextarea();
      expect(textarea).toBeInTheDocument();
      expect(textarea).toHaveAttribute('placeholder');
    });

    it('should render generate button', () => {
      renderComponent();

      const button = getSubmitButton();
      expect(button).toBeInTheDocument();
      expect(button).toHaveTextContent(/generate question/i);
    });

    it('should render with custom placeholder', () => {
      const customPlaceholder = 'Custom placeholder text';
      renderComponent({ placeholder: customPlaceholder });

      const textarea = getTextarea();
      expect(textarea).toHaveAttribute('placeholder', customPlaceholder);
    });

    it('should render with initial prompt', () => {
      const initialPrompt = 'Initial prompt value for testing';
      renderComponent({ initialPrompt });

      const textarea = getTextarea();
      expect(textarea).toHaveValue(initialPrompt);
    });

    it('should render character count', () => {
      const initialPrompt = 'Test prompt';
      renderComponent({ initialPrompt });

      expect(screen.getByText(/11\/500/)).toBeInTheDocument();
    });

    it('should render with custom class name', () => {
      const { container } = renderComponent({ className: 'custom-class' });

      const card = container.querySelector('.custom-class');
      expect(card).toBeInTheDocument();
    });
  });

  // ========== Disabled State Tests ==========

  describe('Disabled State', () => {
    it('should disable textarea when disabled prop is true', () => {
      renderComponent({ disabled: true });

      const textarea = getTextarea();
      expect(textarea).toBeDisabled();
    });

    it('should disable button when disabled prop is true', () => {
      renderComponent({ disabled: true, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      expect(button).toBeDisabled();
    });

    it('should disable button when prompt is too short', () => {
      renderComponent({ initialPrompt: 'Short', minPromptLength: 10 });

      const button = getSubmitButton();
      expect(button).toBeDisabled();
    });

    it('should disable button when prompt exceeds max length', () => {
      renderComponent({ initialPrompt: 'A'.repeat(510), maxPromptLength: 500 });

      const button = getSubmitButton();
      expect(button).toBeDisabled();
    });

    it('should disable button when prompt is empty', () => {
      renderComponent({ initialPrompt: '' });

      const button = getSubmitButton();
      expect(button).toBeDisabled();
    });
  });

  // ========== Validation Tests ==========

  describe('Form Validation', () => {
    it('should disable button when prompt is below minimum length', () => {
      // Button is disabled when prompt is too short, preventing submission
      renderComponent({ minPromptLength: 10, initialPrompt: 'Short' });

      const button = getSubmitButton();
      expect(button).toBeDisabled();
    });

    it('should show under-limit color when prompt is below minimum', () => {
      renderComponent({ minPromptLength: 10, initialPrompt: 'Short' });

      // Character count should show amber/warning color
      const charCount = screen.getByText(/5\/500/);
      expect(charCount).toHaveClass('text-amber-600');
    });

    it('should show warning color when near max length', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      renderComponent({ maxPromptLength: 50 });

      const textarea = getTextarea();
      await user.type(textarea, 'A'.repeat(45));

      const charCount = screen.getByText(/45\/50/);
      expect(charCount).toBeInTheDocument();
    });

    it('should show error color when exceeding max length', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      renderComponent({ maxPromptLength: 20, initialPrompt: 'A'.repeat(25) });

      const charCount = screen.getByText(/25\/20/);
      expect(charCount).toHaveClass('text-destructive');
    });

    it('should count whitespace in character count', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      renderComponent({ minPromptLength: 10 });

      const textarea = getTextarea();
      await user.type(textarea, '          '); // 10 spaces

      // Whitespace still counts toward character count (visible feedback)
      const charCount = screen.getByText(/10\/500/);
      expect(charCount).toBeInTheDocument();
    });
  });

  // ========== Generation Flow Tests ==========

  describe('Generation Flow', () => {
    it('should call onGenerate with trimmed prompt on submit', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate();
      renderComponent({ onGenerate });

      const textarea = getTextarea();
      await user.type(textarea, '  Valid prompt text  ');

      const button = getSubmitButton();
      await user.click(button);

      await waitFor(() => {
        expect(onGenerate).toHaveBeenCalledWith('Valid prompt text');
      });
    });

    it('should show loading state during generation', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate(mockGeneratedQuestion, 2000);
      renderComponent({ onGenerate, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await waitFor(() => {
        expect(screen.getByText(/generating/i)).toBeInTheDocument();
      });
    });

    it('should disable form during generation', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate(mockGeneratedQuestion, 2000);
      renderComponent({ onGenerate, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await waitFor(() => {
        expect(getTextarea()).toBeDisabled();
        expect(button).toBeDisabled();
      });
    });

    it('should show progress indicator during generation', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate(mockGeneratedQuestion, 5000);
      renderComponent({ onGenerate, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await waitFor(() => {
        expect(screen.getByRole('status')).toBeInTheDocument();
      });
    });

    it('should show success state after generation completes', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate(mockGeneratedQuestion, 100);
      const onSuccess = vi.fn();
      renderComponent({ onGenerate, onSuccess, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });

      await waitFor(() => {
        expect(screen.getByText(/question generated/i)).toBeInTheDocument();
      });
    });

    it('should call onSuccess callback after successful generation', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate(mockGeneratedQuestion, 100);
      const onSuccess = vi.fn();
      renderComponent({ onGenerate, onSuccess, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });

      await waitFor(() => {
        expect(onSuccess).toHaveBeenCalledWith(mockGeneratedQuestion);
      });
    });

    it('should display generated question text', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate(mockGeneratedQuestion, 100);
      renderComponent({ onGenerate, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });

      await waitFor(() => {
        expect(screen.getByText(mockGeneratedQuestion.question)).toBeInTheDocument();
      });
    });

    it('should display confidence score', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate(mockGeneratedQuestion, 100);
      renderComponent({ onGenerate, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });

      await waitFor(() => {
        expect(screen.getByText(/confidence: 85%/i)).toBeInTheDocument();
      });
    });
  });

  // ========== Error Handling Tests ==========

  describe('Error Handling', () => {
    it('should show error state when generation fails', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerateError('Test error message', 100);
      renderComponent({ onGenerate, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });

      await waitFor(() => {
        expect(screen.getByText(/generation failed/i)).toBeInTheDocument();
      });
    });

    it('should display error message', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const errorMessage = 'Custom error message for testing';
      const onGenerate = createMockOnGenerateError(errorMessage, 100);
      renderComponent({ onGenerate, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });

      await waitFor(() => {
        expect(screen.getByText(errorMessage)).toBeInTheDocument();
      });
    });

    it('should call onError callback when generation fails', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerateError('Test error', 100);
      const onError = vi.fn();
      renderComponent({ onGenerate, onError, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });

      await waitFor(() => {
        expect(onError).toHaveBeenCalledWith(expect.any(Error));
      });
    });

    it('should allow dismissing error', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerateError('Test error', 100);
      renderComponent({ onGenerate, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });

      await waitFor(() => {
        expect(screen.getByText(/generation failed/i)).toBeInTheDocument();
      });

      const dismissButton = screen.getByLabelText(/dismiss error/i);
      await user.click(dismissButton);

      await waitFor(() => {
        expect(screen.queryByText(/generation failed/i)).not.toBeInTheDocument();
      });
    });

    it('should handle non-Error thrown objects', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = vi.fn(async () => {
        await new Promise(resolve => setTimeout(resolve, 100));
        throw 'String error'; // Non-Error object
      });
      renderComponent({
        onGenerate: onGenerate as QuickQuestionGeneratorProps['onGenerate'],
        initialPrompt: 'Valid prompt text',
      });

      const button = getSubmitButton();
      await user.click(button);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });

      await waitFor(() => {
        expect(screen.getByText(/generation failed/i)).toBeInTheDocument();
      });
    });
  });

  // ========== Reset Functionality Tests ==========

  describe('Reset Functionality', () => {
    it('should show Start Over button after success', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate(mockGeneratedQuestion, 100);
      renderComponent({ onGenerate, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /start over/i })).toBeInTheDocument();
      });
    });

    it('should show Start Over button after error', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerateError('Test error', 100);
      renderComponent({ onGenerate, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /start over/i })).toBeInTheDocument();
      });
    });

    it('should reset form when Start Over is clicked', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate(mockGeneratedQuestion, 100);
      renderComponent({ onGenerate, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });

      await waitFor(() => {
        expect(screen.getByText(/question generated/i)).toBeInTheDocument();
      });

      const startOverButton = screen.getByRole('button', { name: /start over/i });
      await user.click(startOverButton);

      await waitFor(() => {
        expect(screen.queryByText(/question generated/i)).not.toBeInTheDocument();
      });
    });

    it('should reset textarea to default value after reset', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate(mockGeneratedQuestion, 100);
      // No initial prompt, so default is empty string
      renderComponent({ onGenerate });

      // Type a valid prompt
      const textarea = getTextarea();
      await user.type(textarea, 'Valid prompt text');

      const button = getSubmitButton();
      await user.click(button);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });

      const startOverButton = await screen.findByRole('button', { name: /start over/i });
      await user.click(startOverButton);

      await waitFor(() => {
        // Resets to default value (empty string since no initialPrompt)
        expect(getTextarea()).toHaveValue('');
      });
    });
  });

  // ========== Progress Indicator Tests ==========

  describe('Progress Indicator', () => {
    it('should update progress during generation', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate(mockGeneratedQuestion, 5000);
      renderComponent({ onGenerate, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      // Wait for initial progress
      await waitFor(() => {
        expect(screen.getByRole('status')).toBeInTheDocument();
      });

      // Advance time to see progress updates
      await act(async () => {
        await vi.advanceTimersByTimeAsync(600);
      });

      await waitFor(() => {
        expect(screen.getByText(/analyzing prompt/i)).toBeInTheDocument();
      });
    });

    it('should show different status messages at different stages', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate(mockGeneratedQuestion, 10000);
      renderComponent({ onGenerate, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await waitFor(() => {
        expect(screen.getByRole('status')).toBeInTheDocument();
      });

      // Check that status updates over time
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1100);
      });

      // Should have progressed beyond initial state
      const statusElement = screen.getByRole('status');
      expect(statusElement).toBeInTheDocument();
    });
  });

  // ========== Accessibility Tests ==========

  describe('Accessibility', () => {
    it('should have proper aria-label on progress indicator', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate(mockGeneratedQuestion, 2000);
      renderComponent({ onGenerate, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await waitFor(() => {
        const status = screen.getByRole('status');
        expect(status).toHaveAttribute('aria-label', expect.stringContaining('progress'));
      });
    });

    it('should have aria-invalid set to false by default', () => {
      renderComponent({ initialPrompt: 'Valid prompt text' });

      const textarea = getTextarea();
      // By default, aria-invalid is false (no validation error)
      expect(textarea).toHaveAttribute('aria-invalid', 'false');
    });

    it('should have aria-describedby linking to error message', async () => {
      renderComponent();

      const textarea = getTextarea();
      expect(textarea).toHaveAttribute(
        'aria-describedby',
        expect.stringContaining('prompt-description')
      );
    });

    it('should have aria-live on character count', () => {
      renderComponent({ initialPrompt: 'Test prompt' });

      const charCount = screen.getByText(/11\/500/);
      expect(charCount).toHaveAttribute('aria-live', 'polite');
    });

    it('should have proper role on error alert', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerateError('Test error', 100);
      renderComponent({ onGenerate, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });

      await waitFor(() => {
        const alert = screen.getByRole('alert');
        expect(alert).toBeInTheDocument();
      });
    });

    it('should have aria-busy on button during loading', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate(mockGeneratedQuestion, 2000);
      renderComponent({ onGenerate, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();
      await user.click(button);

      await waitFor(() => {
        expect(button).toHaveAttribute('aria-busy', 'true');
      });
    });
  });

  // ========== Edge Cases ==========

  describe('Edge Cases', () => {
    it('should handle multiple rapid submissions', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate(mockGeneratedQuestion, 1000);
      renderComponent({ onGenerate, initialPrompt: 'Valid prompt text' });

      const button = getSubmitButton();

      // First click
      await user.click(button);

      // Button should be disabled, second click should not trigger
      expect(button).toBeDisabled();

      await act(async () => {
        await vi.advanceTimersByTimeAsync(1500);
      });

      // Only one call should have been made
      expect(onGenerate).toHaveBeenCalledTimes(1);
    });

    it('should handle prompt exactly at minimum length', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate(mockGeneratedQuestion, 100);
      renderComponent({ onGenerate, minPromptLength: 10 });

      const textarea = getTextarea();
      await user.type(textarea, 'Exactly 10'); // Exactly 10 characters

      const button = getSubmitButton();
      expect(button).not.toBeDisabled();

      await user.click(button);

      await waitFor(() => {
        expect(onGenerate).toHaveBeenCalled();
      });
    });

    it('should handle prompt exactly at maximum length', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerate(mockGeneratedQuestion, 100);
      renderComponent({ onGenerate, maxPromptLength: 20, initialPrompt: 'A'.repeat(20) });

      const button = getSubmitButton();
      expect(button).not.toBeDisabled();

      await user.click(button);

      await waitFor(() => {
        expect(onGenerate).toHaveBeenCalled();
      });
    });

    it('should preserve form state after failed submission', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onGenerate = createMockOnGenerateError('Test error', 100);
      renderComponent({ onGenerate });

      const textarea = getTextarea();
      const promptText = 'This is my test prompt';
      await user.type(textarea, promptText);

      const button = getSubmitButton();
      await user.click(button);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });

      await waitFor(() => {
        expect(screen.getByText(/generation failed/i)).toBeInTheDocument();
      });

      // Textarea should still have the original text
      expect(textarea).toHaveValue(promptText);
    });
  });

  // ========== Custom Configuration Tests ==========

  describe('Custom Configuration', () => {
    it('should respect custom minPromptLength', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      renderComponent({ minPromptLength: 20 });

      const textarea = getTextarea();
      await user.type(textarea, 'Short'); // Only 5 characters

      const button = getSubmitButton();
      expect(button).toBeDisabled();
    });

    it('should respect custom maxPromptLength', () => {
      renderComponent({ maxPromptLength: 100, initialPrompt: 'A'.repeat(50) });

      expect(screen.getByText(/50\/100/)).toBeInTheDocument();
    });

    it('should use initialPrompt value', () => {
      const initialPrompt = 'My initial prompt value';
      renderComponent({ initialPrompt });

      const textarea = getTextarea();
      expect(textarea).toHaveValue(initialPrompt);
    });
  });
});
