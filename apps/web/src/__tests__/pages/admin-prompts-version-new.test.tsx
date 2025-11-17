import React from 'react';
import {  screen, fireEvent, waitFor, act } from '@testing-library/react';
import { renderWithQuery } from '../utils/query-test-utils';
import { useRouter } from 'next/router';
import NewPromptVersion from '../../pages/admin/prompts/[id]/versions/new';
import { api } from '../../lib/api';

// Mock dependencies
jest.mock('next/router', () => ({
  useRouter: jest.fn(),
}));

jest.mock('next/link', () => {
  return ({ children, href }: any) => {
    return <a href={href}>{children}</a>;
  };
});

jest.mock('../../lib/api', () => ({
  api: {
    post: jest.fn(),
  },
}));

jest.mock('../../components/PromptEditor', () => {
  return function MockPromptEditor({ value, onChange, placeholder }: any) {
    return (
      <textarea
        data-testid="prompt-editor"
        value={value}
        onChange={(e) => onChange?.(e.target.value)}
        placeholder={placeholder}
      />
    );
  };
});

const mockPush = jest.fn();
const mockRouter = {
  push: mockPush,
  query: { id: 'template-123' },
};

describe('NewPromptVersion Page', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
    (useRouter as jest.Mock).mockReturnValue(mockRouter);
  });

  afterEach(async () => {
    await act(async () => {
      jest.runOnlyPendingTimers();
    });
    jest.useRealTimers();
  });

  describe('Rendering', () => {
    it('should render the page with all form fields', () => {
      renderWithQuery(<NewPromptVersion />);

      expect(screen.getByText('Create New Version')).toBeInTheDocument();
      expect(screen.getByText(/Prompt Content/)).toBeInTheDocument(); // Label text
      expect(screen.getByPlaceholderText(/temperature/)).toBeInTheDocument();
      expect(
        screen.getByRole('checkbox', { name: /Activate this version immediately/ })
      ).toBeInTheDocument();
      expect(screen.getByText('Create Version')).toBeInTheDocument();
      expect(screen.getByText('Cancel')).toBeInTheDocument();
    });

    it('should render back button with correct link', () => {
      renderWithQuery(<NewPromptVersion />);

      const backButton = screen.getByText('← Back to Template');
      expect(backButton.closest('a')).toHaveAttribute(
        'href',
        '/admin/prompts/template-123'
      );
    });

    it('should render cancel button with correct link', () => {
      renderWithQuery(<NewPromptVersion />);

      const cancelButton = screen.getByText('Cancel');
      expect(cancelButton.closest('a')).toHaveAttribute(
        'href',
        '/admin/prompts/template-123'
      );
    });
  });

  describe('Form Validation', () => {
    it('should show error when submitting without content', async () => {
      renderWithQuery(<NewPromptVersion />);

      const form = screen.getByText('Create Version').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(screen.getByText('Content is required')).toBeInTheDocument();
        expect(
          screen.getByText('Please fix the errors before submitting')
        ).toBeInTheDocument();
      });

      expect(api.post).not.toHaveBeenCalled();
    });

    it('should show error for invalid JSON metadata', async () => {
      renderWithQuery(<NewPromptVersion />);

      const contentEditor = screen.getByTestId('prompt-editor');
      fireEvent.change(contentEditor, { target: { value: 'Valid content' } });

      const metadataTextarea = screen.getByPlaceholderText(/temperature/);
      fireEvent.change(metadataTextarea, { target: { value: 'invalid json' } });

      const form = screen.getByText('Create Version').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(screen.getByText('Metadata must be valid JSON')).toBeInTheDocument();
      });

      expect(api.post).not.toHaveBeenCalled();
    });

    it('should accept valid JSON metadata', async () => {
      (api.post as jest.Mock).mockResolvedValue({ id: 'version-123' });

      renderWithQuery(<NewPromptVersion />);

      const contentEditor = screen.getByTestId('prompt-editor');
      fireEvent.change(contentEditor, { target: { value: 'Valid content' } });

      const metadataTextarea = screen.getByPlaceholderText(/temperature/);
      fireEvent.change(metadataTextarea, {
        target: { value: '{"temperature": 0.7}' },
      });

      const form = screen.getByText('Create Version').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(api.post).toHaveBeenCalledWith(
          '/api/v1/admin/prompts/template-123/versions',
          {
            content: 'Valid content',
            metadata: { temperature: 0.7 },
          }
        );
      });
    });

    it('should allow empty metadata', async () => {
      (api.post as jest.Mock).mockResolvedValue({ id: 'version-123' });

      renderWithQuery(<NewPromptVersion />);

      const contentEditor = screen.getByTestId('prompt-editor');
      fireEvent.change(contentEditor, { target: { value: 'Valid content' } });

      const form = screen.getByText('Create Version').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(api.post).toHaveBeenCalledWith(
          '/api/v1/admin/prompts/template-123/versions',
          {
            content: 'Valid content',
          }
        );
      });
    });
  });

  describe('Form Submission', () => {
    it('should create version successfully without activation', async () => {
      (api.post as jest.Mock).mockResolvedValue({ id: 'version-123' });

      renderWithQuery(<NewPromptVersion />);

      const contentEditor = screen.getByTestId('prompt-editor');
      fireEvent.change(contentEditor, {
        target: { value: 'System prompt content' },
      });

      const form = screen.getByText('Create Version').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(api.post).toHaveBeenCalledWith(
          '/api/v1/admin/prompts/template-123/versions',
          {
            content: 'System prompt content',
          }
        );
      });

      await waitFor(() => {
        expect(screen.getByText('Version created successfully')).toBeInTheDocument();
      });

      act(() => {
        jest.advanceTimersByTime(1000);
      });

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/admin/prompts/template-123');
      });
    });

    it('should create and activate version when checkbox is checked', async () => {
      (api.post as jest.Mock).mockResolvedValue({ id: 'version-123' });

      renderWithQuery(<NewPromptVersion />);

      const contentEditor = screen.getByTestId('prompt-editor');
      fireEvent.change(contentEditor, {
        target: { value: 'System prompt content' },
      });

      const activateCheckbox = screen.getByRole('checkbox', {
        name: /Activate this version immediately/
      });
      fireEvent.click(activateCheckbox);

      const form = screen.getByText('Create Version').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(api.post).toHaveBeenCalledWith(
          '/api/v1/admin/prompts/template-123/versions',
          {
            content: 'System prompt content',
          }
        );
      });

      await waitFor(() => {
        expect(api.post).toHaveBeenCalledWith(
          '/api/v1/admin/prompts/template-123/versions/version-123/activate',
          {}
        );
      });

      await waitFor(() => {
        expect(screen.getByText('Version created and activated')).toBeInTheDocument();
      });
    });

    it('should handle activation failure gracefully', async () => {
      (api.post as jest.Mock)
        .mockResolvedValueOnce({ id: 'version-123' })
        .mockRejectedValueOnce(new Error('Activation failed'));

      const consoleSpy = jest.spyOn(console, 'error').mockImplementation();

      renderWithQuery(<NewPromptVersion />);

      const contentEditor = screen.getByTestId('prompt-editor');
      fireEvent.change(contentEditor, {
        target: { value: 'System prompt content' },
      });

      const activateCheckbox = screen.getByRole('checkbox', {
        name: /Activate this version immediately/
      });
      fireEvent.click(activateCheckbox);

      const form = screen.getByText('Create Version').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(consoleSpy).toHaveBeenCalledWith(
          'Failed to activate version:',
          expect.any(Error)
        );
      });

      consoleSpy.mockRestore();
    });

    it('should show loading state during submission', async () => {
      (api.post as jest.Mock).mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve({ id: '123' }), 100))
      );

      renderWithQuery(<NewPromptVersion />);

      const contentEditor = screen.getByTestId('prompt-editor');
      fireEvent.change(contentEditor, { target: { value: 'Content' } });

      const submitButton = screen.getByText('Create Version');
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText('Creating...')).toBeInTheDocument();
        expect(submitButton).toBeDisabled();
      });
    });

    it('should handle creation error', async () => {
      (api.post as jest.Mock).mockRejectedValue(new Error('Network error'));

      renderWithQuery(<NewPromptVersion />);

      const contentEditor = screen.getByTestId('prompt-editor');
      fireEvent.change(contentEditor, { target: { value: 'Content' } });

      const form = screen.getByText('Create Version').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(screen.getByText('Network error')).toBeInTheDocument();
      });

      expect(mockPush).not.toHaveBeenCalled();
    });

    it('should handle null API response', async () => {
      (api.post as jest.Mock).mockResolvedValue(null);

      renderWithQuery(<NewPromptVersion />);

      const contentEditor = screen.getByTestId('prompt-editor');
      fireEvent.change(contentEditor, { target: { value: 'Content' } });

      const form = screen.getByText('Create Version').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(screen.getByText('Failed to create version')).toBeInTheDocument();
      });
    });
  });

  describe('Content Editor', () => {
    it('should update content when editor changes', () => {
      renderWithQuery(<NewPromptVersion />);

      const contentEditor = screen.getByTestId('prompt-editor');
      fireEvent.change(contentEditor, {
        target: { value: 'New prompt content' },
      });

      expect(contentEditor).toHaveValue('New prompt content');
    });

    it('should display placeholder in editor', () => {
      renderWithQuery(<NewPromptVersion />);

      const contentEditor = screen.getByTestId('prompt-editor');
      expect(contentEditor).toHaveAttribute(
        'placeholder',
        'Enter your prompt content here...'
      );
    });
  });

  describe('Metadata Editor', () => {
    it('should update metadata when textarea changes', () => {
      renderWithQuery(<NewPromptVersion />);

      const metadataTextarea = screen.getByPlaceholderText(/temperature/);
      const jsonValue = '{"temperature": 0.7, "max_tokens": 1000}';
      fireEvent.change(metadataTextarea, { target: { value: jsonValue } });

      expect(metadataTextarea).toHaveValue(jsonValue);
    });

    it('should clear errors when valid JSON is entered', async () => {
      renderWithQuery(<NewPromptVersion />);

      const contentEditor = screen.getByTestId('prompt-editor');
      fireEvent.change(contentEditor, { target: { value: 'Content' } });

      const metadataTextarea = screen.getByPlaceholderText(/temperature/);
      fireEvent.change(metadataTextarea, { target: { value: 'invalid' } });

      const form = screen.getByText('Create Version').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(screen.getByText('Metadata must be valid JSON')).toBeInTheDocument();
      });

      fireEvent.change(metadataTextarea, { target: { value: '{}' } });
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(
          screen.queryByText('Metadata must be valid JSON')
        ).not.toBeInTheDocument();
      });
    });
  });

  describe('Activate Checkbox', () => {
    it('should toggle activation checkbox', () => {
      renderWithQuery(<NewPromptVersion />);

      const checkbox = screen.getByRole('checkbox', { name: /Activate this version immediately/ });
      expect(checkbox).not.toBeChecked();

      fireEvent.click(checkbox);
      expect(checkbox).toBeChecked();

      fireEvent.click(checkbox);
      expect(checkbox).not.toBeChecked();
    });
  });

  describe('Toast Notifications', () => {
    it('should auto-hide success toast after 5 seconds', async () => {
      (api.post as jest.Mock).mockResolvedValue({ id: 'version-123' });

      renderWithQuery(<NewPromptVersion />);

      const contentEditor = screen.getByTestId('prompt-editor');
      fireEvent.change(contentEditor, { target: { value: 'Content' } });

      const form = screen.getByText('Create Version').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(screen.getByText('Version created successfully')).toBeInTheDocument();
      });

      act(() => {
        jest.advanceTimersByTime(5000);
      });

      await waitFor(() => {
        expect(
          screen.queryByText('Version created successfully')
        ).not.toBeInTheDocument();
      });
    });

    it('should auto-hide error toast after 5 seconds', async () => {
      (api.post as jest.Mock).mockRejectedValue(new Error('API error'));

      renderWithQuery(<NewPromptVersion />);

      const contentEditor = screen.getByTestId('prompt-editor');
      fireEvent.change(contentEditor, { target: { value: 'Content' } });

      const form = screen.getByText('Create Version').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(screen.getByText('API error')).toBeInTheDocument();
      });

      act(() => {
        jest.advanceTimersByTime(5000);
      });

      await waitFor(() => {
        expect(screen.queryByText('API error')).not.toBeInTheDocument();
      });
    });
  });

  describe('Edge Cases', () => {
    it('should handle missing router query id', () => {
      (useRouter as jest.Mock).mockReturnValue({
        ...mockRouter,
        query: {},
      });

      renderWithQuery(<NewPromptVersion />);

      // Should still render the form
      expect(screen.getByText('Create New Version')).toBeInTheDocument();
    });

    it('should trim whitespace from content before validation', async () => {
      renderWithQuery(<NewPromptVersion />);

      const contentEditor = screen.getByTestId('prompt-editor');
      fireEvent.change(contentEditor, { target: { value: '   ' } });

      const form = screen.getByText('Create Version').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(screen.getByText('Content is required')).toBeInTheDocument();
      });
    });

    it('should handle complex nested JSON metadata', async () => {
      (api.post as jest.Mock).mockResolvedValue({ id: 'version-123' });

      renderWithQuery(<NewPromptVersion />);

      const contentEditor = screen.getByTestId('prompt-editor');
      fireEvent.change(contentEditor, { target: { value: 'Content' } });

      const metadataTextarea = screen.getByPlaceholderText(/temperature/);
      const complexJson = JSON.stringify({
        model: {
          temperature: 0.7,
          max_tokens: 1000,
          top_p: 0.9,
        },
        tags: ['production', 'v2'],
        features: {
          streaming: true,
          caching: false,
        },
      });
      fireEvent.change(metadataTextarea, { target: { value: complexJson } });

      const form = screen.getByText('Create Version').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(api.post).toHaveBeenCalledWith(
          expect.any(String),
          expect.objectContaining({
            content: 'Content',
            metadata: expect.objectContaining({
              model: expect.any(Object),
              tags: expect.any(Array),
              features: expect.any(Object),
            }),
          })
        );
      });
    });
  });
});
