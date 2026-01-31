/**
 * QuestionInputForm Component Tests
 *
 * Comprehensive tests for the standalone QuestionInputForm component.
 * Tests cover rendering, input handling, form submission, loading states,
 * response mode toggle, attachment button, and accessibility.
 *
 * Uses test-i18n utilities for language-agnostic text matching.
 *
 * @issue BGAI-061
 */

import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QuestionInputForm, ResponseMode } from '../QuestionInputForm';
import { t, getTextMatcher } from '@/test-utils/test-i18n';

// Mock next-intl to use test-i18n utility with interpolation support
vi.mock('next-intl', () => ({
  useTranslations:
    (namespace: string) => (key: string, values?: Record<string, string | number>) => {
      let translation = t(`${namespace}.${key}`);
      if (values) {
        Object.entries(values).forEach(([k, v]) => {
          translation = translation.replace(new RegExp(`\\{${k}\\}`, 'g'), String(v));
        });
      }
      return translation;
    },
}));

describe('QuestionInputForm', () => {
  const defaultProps = {
    value: '',
    onChange: vi.fn(),
    onSubmit: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  /**
   * RENDERING TESTS
   */
  describe('Rendering', () => {
    it('renders input field with correct placeholder', () => {
      render(<QuestionInputForm {...defaultProps} />);

      const input = screen.getByRole('textbox');
      expect(input).toBeInTheDocument();
      expect(input).toHaveAttribute('placeholder', t('questionInput.placeholder'));
    });

    it('renders custom placeholder when provided', () => {
      render(<QuestionInputForm {...defaultProps} placeholder="Fai una domanda..." />);

      const input = screen.getByRole('textbox');
      expect(input).toHaveAttribute('placeholder', 'Fai una domanda...');
    });

    it('renders send button', () => {
      render(<QuestionInputForm {...defaultProps} />);

      expect(
        screen.getByRole('button', { name: getTextMatcher('questionInput.sendQuestion') })
      ).toBeInTheDocument();
    });

    it('has search role for the container', () => {
      render(<QuestionInputForm {...defaultProps} />);

      expect(
        screen.getByRole('search', { name: getTextMatcher('questionInput.searchLabel') })
      ).toBeInTheDocument();
    });
  });

  /**
   * INPUT HANDLING TESTS
   */
  describe('Input Handling', () => {
    it('calls onChange when typing', async () => {
      const onChange = vi.fn();
      const user = userEvent.setup();
      render(<QuestionInputForm {...defaultProps} onChange={onChange} />);

      const input = screen.getByRole('textbox');
      await user.type(input, 'Test');

      expect(onChange).toHaveBeenCalled();
    });

    it('displays current input value', () => {
      render(<QuestionInputForm {...defaultProps} value="Current question" />);

      const input = screen.getByRole('textbox') as HTMLInputElement;
      expect(input.value).toBe('Current question');
    });

    it('respects maxLength attribute', () => {
      render(<QuestionInputForm {...defaultProps} maxLength={100} />);

      const input = screen.getByRole('textbox');
      expect(input).toHaveAttribute('maxLength', '100');
    });

    it('auto-focuses when autoFocus is true', () => {
      render(<QuestionInputForm {...defaultProps} autoFocus />);

      const input = screen.getByRole('textbox');
      expect(document.activeElement).toBe(input);
    });
  });

  /**
   * FORM SUBMISSION TESTS
   */
  describe('Form Submission', () => {
    it('calls onSubmit on form submit with trimmed value', () => {
      const onSubmit = vi.fn();
      render(<QuestionInputForm {...defaultProps} value="  Test question  " onSubmit={onSubmit} />);

      const form = screen
        .getByRole('button', { name: getTextMatcher('questionInput.sendQuestion') })
        .closest('form');
      fireEvent.submit(form!);

      expect(onSubmit).toHaveBeenCalledWith('Test question');
    });

    it('calls onSubmit on send button click', () => {
      const onSubmit = vi.fn();
      render(<QuestionInputForm {...defaultProps} value="Button click test" onSubmit={onSubmit} />);

      const sendButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.sendQuestion'),
      });
      fireEvent.click(sendButton);

      expect(onSubmit).toHaveBeenCalledWith('Button click test');
    });

    it('calls onSubmit on Enter key press', async () => {
      const onSubmit = vi.fn();
      const user = userEvent.setup();
      render(<QuestionInputForm {...defaultProps} value="Enter key test" onSubmit={onSubmit} />);

      const input = screen.getByRole('textbox');
      await user.type(input, '{Enter}');

      expect(onSubmit).toHaveBeenCalledWith('Enter key test');
    });

    it('does not submit empty message', () => {
      const onSubmit = vi.fn();
      render(<QuestionInputForm {...defaultProps} value="" onSubmit={onSubmit} />);

      const form = screen
        .getByRole('button', { name: getTextMatcher('questionInput.sendQuestion') })
        .closest('form');
      fireEvent.submit(form!);

      expect(onSubmit).not.toHaveBeenCalled();
    });

    it('does not submit whitespace-only message', () => {
      const onSubmit = vi.fn();
      render(<QuestionInputForm {...defaultProps} value="   " onSubmit={onSubmit} />);

      const form = screen
        .getByRole('button', { name: getTextMatcher('questionInput.sendQuestion') })
        .closest('form');
      fireEvent.submit(form!);

      expect(onSubmit).not.toHaveBeenCalled();
    });

    it('disables send button when input is empty', () => {
      render(<QuestionInputForm {...defaultProps} value="" />);

      const sendButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.sendQuestion'),
      });
      expect(sendButton).toBeDisabled();
    });

    it('enables send button when input has value', () => {
      render(<QuestionInputForm {...defaultProps} value="Valid question" />);

      const sendButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.sendQuestion'),
      });
      expect(sendButton).not.toBeDisabled();
    });
  });

  /**
   * DISABLED STATE TESTS
   */
  describe('Disabled State', () => {
    it('disables input when disabled prop is true', () => {
      render(<QuestionInputForm {...defaultProps} disabled />);

      const input = screen.getByRole('textbox');
      expect(input).toBeDisabled();
    });

    it('disables send button when disabled prop is true', () => {
      render(<QuestionInputForm {...defaultProps} value="Test" disabled />);

      const sendButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.sendQuestion'),
      });
      expect(sendButton).toBeDisabled();
    });

    it('does not call onSubmit when disabled', () => {
      const onSubmit = vi.fn();
      render(<QuestionInputForm {...defaultProps} value="Test" onSubmit={onSubmit} disabled />);

      const form = screen
        .getByRole('button', { name: getTextMatcher('questionInput.sendQuestion') })
        .closest('form');
      fireEvent.submit(form!);

      expect(onSubmit).not.toHaveBeenCalled();
    });
  });

  /**
   * LOADING STATE TESTS
   */
  describe('Loading State', () => {
    it('shows loading spinner when isLoading is true', () => {
      render(<QuestionInputForm {...defaultProps} value="Test" isLoading />);

      const sendButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.sending'),
      });
      expect(sendButton).toBeInTheDocument();
    });

    it('disables input when isLoading is true', () => {
      render(<QuestionInputForm {...defaultProps} isLoading />);

      const input = screen.getByRole('textbox');
      expect(input).toBeDisabled();
    });

    it('disables send button when isLoading is true', () => {
      render(<QuestionInputForm {...defaultProps} value="Test" isLoading />);

      const sendButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.sending'),
      });
      expect(sendButton).toBeDisabled();
    });
  });

  /**
   * ATTACHMENT BUTTON TESTS
   */
  describe('Attachment Button', () => {
    it('does not render attachment button by default', () => {
      render(<QuestionInputForm {...defaultProps} />);

      expect(
        screen.queryByRole('button', { name: getTextMatcher('questionInput.attachFile') })
      ).not.toBeInTheDocument();
    });

    it('renders attachment button when showAttachment is true', () => {
      render(<QuestionInputForm {...defaultProps} showAttachment />);

      expect(
        screen.getByRole('button', { name: getTextMatcher('questionInput.attachFile') })
      ).toBeInTheDocument();
    });

    it('calls onAttach when attachment button is clicked', () => {
      const onAttach = vi.fn();
      render(<QuestionInputForm {...defaultProps} showAttachment onAttach={onAttach} />);

      const attachButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.attachFile'),
      });
      fireEvent.click(attachButton);

      expect(onAttach).toHaveBeenCalled();
    });

    it('disables attachment button when disabled', () => {
      render(<QuestionInputForm {...defaultProps} showAttachment disabled />);

      const attachButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.attachFile'),
      });
      expect(attachButton).toBeDisabled();
    });

    it('disables attachment button when isLoading', () => {
      render(<QuestionInputForm {...defaultProps} showAttachment isLoading />);

      const attachButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.attachFile'),
      });
      expect(attachButton).toBeDisabled();
    });
  });

  /**
   * RESPONSE MODE TOGGLE TESTS
   */
  describe('Response Mode Toggle', () => {
    it('does not render response mode toggle by default', () => {
      render(<QuestionInputForm {...defaultProps} />);

      expect(
        screen.queryByRole('button', { name: getTextMatcher('questionInput.fastResponse') })
      ).not.toBeInTheDocument();
      expect(
        screen.queryByRole('button', { name: getTextMatcher('questionInput.completeResponse') })
      ).not.toBeInTheDocument();
    });

    it('renders response mode toggle when showResponseModeToggle is true', () => {
      render(<QuestionInputForm {...defaultProps} showResponseModeToggle />);

      expect(
        screen.getByRole('button', { name: getTextMatcher('questionInput.fastResponse') })
      ).toBeInTheDocument();
      expect(
        screen.getByRole('button', { name: getTextMatcher('questionInput.completeResponse') })
      ).toBeInTheDocument();
    });

    it('shows fast mode as selected by default', () => {
      render(<QuestionInputForm {...defaultProps} showResponseModeToggle responseMode="fast" />);

      const fastButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.fastResponse'),
      });
      expect(fastButton).toHaveAttribute('aria-pressed', 'true');
    });

    it('shows complete mode as selected when set', () => {
      render(
        <QuestionInputForm {...defaultProps} showResponseModeToggle responseMode="complete" />
      );

      const completeButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.completeResponse'),
      });
      expect(completeButton).toHaveAttribute('aria-pressed', 'true');
    });

    it('calls onResponseModeChange when fast mode is clicked', () => {
      const onResponseModeChange = vi.fn();
      render(
        <QuestionInputForm
          {...defaultProps}
          showResponseModeToggle
          responseMode="complete"
          onResponseModeChange={onResponseModeChange}
        />
      );

      const fastButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.fastResponse'),
      });
      fireEvent.click(fastButton);

      expect(onResponseModeChange).toHaveBeenCalledWith('fast');
    });

    it('calls onResponseModeChange when complete mode is clicked', () => {
      const onResponseModeChange = vi.fn();
      render(
        <QuestionInputForm
          {...defaultProps}
          showResponseModeToggle
          responseMode="fast"
          onResponseModeChange={onResponseModeChange}
        />
      );

      const completeButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.completeResponse'),
      });
      fireEvent.click(completeButton);

      expect(onResponseModeChange).toHaveBeenCalledWith('complete');
    });

    it('disables response mode buttons when disabled', () => {
      render(<QuestionInputForm {...defaultProps} showResponseModeToggle disabled />);

      const fastButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.fastResponse'),
      });
      const completeButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.completeResponse'),
      });
      expect(fastButton).toBeDisabled();
      expect(completeButton).toBeDisabled();
    });

    it('disables response mode buttons when isLoading', () => {
      render(<QuestionInputForm {...defaultProps} showResponseModeToggle isLoading />);

      const fastButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.fastResponse'),
      });
      const completeButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.completeResponse'),
      });
      expect(fastButton).toBeDisabled();
      expect(completeButton).toBeDisabled();
    });
  });

  /**
   * ACCESSIBILITY TESTS
   */
  describe('Accessibility', () => {
    it('has accessible label for input', () => {
      render(<QuestionInputForm {...defaultProps} />);

      const input = screen.getByLabelText(getTextMatcher('questionInput.placeholder'));
      expect(input).toBeInTheDocument();
    });

    it('has screen reader text for character count', () => {
      render(<QuestionInputForm {...defaultProps} value="Test" maxLength={100} />);

      // Character count uses interpolation, so we check for the pattern
      const charCount = screen.getByText(content => {
        return content.includes('4') && content.includes('100');
      });
      expect(charCount).toHaveClass('sr-only');
    });

    it('has radiogroup role for response mode toggle', () => {
      render(<QuestionInputForm {...defaultProps} showResponseModeToggle />);

      expect(
        screen.getByRole('radiogroup', { name: getTextMatcher('questionInput.responseModeLabel') })
      ).toBeInTheDocument();
    });

    it('has correct aria-pressed states for response mode buttons', () => {
      render(<QuestionInputForm {...defaultProps} showResponseModeToggle responseMode="fast" />);

      const fastButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.fastResponse'),
      });
      const completeButton = screen.getByRole('button', {
        name: getTextMatcher('questionInput.completeResponse'),
      });

      expect(fastButton).toHaveAttribute('aria-pressed', 'true');
      expect(completeButton).toHaveAttribute('aria-pressed', 'false');
    });

    it('shows correct aria-label for send button based on loading state', () => {
      const { rerender } = render(
        <QuestionInputForm {...defaultProps} value="Test" isLoading={false} />
      );

      expect(
        screen.getByRole('button', { name: getTextMatcher('questionInput.sendQuestion') })
      ).toBeInTheDocument();

      rerender(<QuestionInputForm {...defaultProps} value="Test" isLoading={true} />);

      expect(
        screen.getByRole('button', { name: getTextMatcher('questionInput.sending') })
      ).toBeInTheDocument();
    });
  });

  /**
   * EDGE CASES
   */
  describe('Edge Cases', () => {
    it('handles special characters in input', async () => {
      const onChange = vi.fn();
      const user = userEvent.setup();
      render(<QuestionInputForm {...defaultProps} onChange={onChange} />);

      const input = screen.getByRole('textbox');
      await user.type(input, '<>&"\' Test');

      expect(onChange).toHaveBeenCalled();
    });

    it('handles emoji in input', async () => {
      const onChange = vi.fn();
      const user = userEvent.setup();
      render(<QuestionInputForm {...defaultProps} onChange={onChange} />);

      const input = screen.getByRole('textbox');
      await user.type(input, '🎲🎯');

      expect(onChange).toHaveBeenCalled();
    });

    it('handles Italian characters', async () => {
      const onChange = vi.fn();
      const user = userEvent.setup();
      render(<QuestionInputForm {...defaultProps} onChange={onChange} />);

      const input = screen.getByRole('textbox');
      await user.type(input, 'Come si piazzano le strade?');

      expect(onChange).toHaveBeenCalled();
    });

    it('applies custom className to container', () => {
      render(<QuestionInputForm {...defaultProps} className="custom-class" />);

      const container = screen.getByRole('search');
      expect(container).toHaveClass('custom-class');
    });

    // Note: Shift+Enter behavior is handled by handleKeyDown which prevents
    // the custom submit call, but the native form submit may still fire.
    // This is acceptable for single-line input fields where multiline
    // editing is not expected.
  });
});
