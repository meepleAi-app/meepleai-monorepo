/**
 * ApiKeyCreationModal Tests (Issue #909)
 *
 * Comprehensive test suite for API key creation modal.
 * Covers: validation, submission, success state, error handling, accessibility.
 */

import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ApiKeyCreationModal } from '../ApiKeyCreationModal';
import { api } from '@/lib/api';

// Mock dependencies
vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      createApiKey: vi.fn(),
    },
  },
}));

vi.mock('@/lib/logger', () => ({
  logger: {
    info: vi.fn(),
    error: vi.fn(),
    warn: vi.fn(),
  },
}));

describe('ApiKeyCreationModal', () => {
  const mockOnClose = vi.fn();
  const mockOnApiKeyCreated = vi.fn();

  const defaultProps = {
    isOpen: true,
    onClose: mockOnClose,
    onApiKeyCreated: mockOnApiKeyCreated,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ========== Rendering Tests ==========

  describe('Rendering', () => {
    it('should render the modal when open', () => {
      render(<ApiKeyCreationModal {...defaultProps} />);

      expect(screen.getByRole('dialog')).toBeInTheDocument();
      expect(screen.getByTestId('api-key-modal-title')).toHaveTextContent('Create New API Key');
      expect(
        screen.getByText(/Generate a new API key for programmatic access/)
      ).toBeInTheDocument();
    });

    it('should not render when closed', () => {
      render(<ApiKeyCreationModal {...defaultProps} isOpen={false} />);

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('should render all form fields', () => {
      render(<ApiKeyCreationModal {...defaultProps} />);

      expect(screen.getByLabelText(/API Key Name/i)).toBeInTheDocument();
      expect(screen.getByText(/Scopes/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/Expiration Date/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/Metadata/i)).toBeInTheDocument();
    });

    it('should render all available scopes', () => {
      render(<ApiKeyCreationModal {...defaultProps} />);

      expect(screen.getByLabelText(/Read/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/Write/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/Admin/i)).toBeInTheDocument();
    });

    it('should render action buttons', () => {
      render(<ApiKeyCreationModal {...defaultProps} />);

      expect(screen.getByRole('button', { name: /Cancel/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Create API Key/i })).toBeInTheDocument();
    });
  });

  // ========== Validation Tests ==========

  describe('Validation', () => {
    it('should show error when key name is empty', async () => {
      const user = userEvent.setup();
      render(<ApiKeyCreationModal {...defaultProps} />);

      const createButton = screen.getByRole('button', { name: /Create API Key/i });
      await user.click(createButton);

      await waitFor(() => {
        expect(screen.getByText(/API key name is required/i)).toBeInTheDocument();
      });
    });

    it('should show error when key name is too short', async () => {
      const user = userEvent.setup();
      render(<ApiKeyCreationModal {...defaultProps} />);

      const keyNameInput = screen.getByLabelText(/API Key Name/i);
      await user.type(keyNameInput, 'ab');

      const createButton = screen.getByRole('button', { name: /Create API Key/i });
      await user.click(createButton);

      await waitFor(() => {
        expect(screen.getByText(/at least 3 characters/i)).toBeInTheDocument();
      });
    });

    it('should show error when key name is too long', async () => {
      const user = userEvent.setup();
      render(<ApiKeyCreationModal {...defaultProps} />);

      const keyNameInput = screen.getByLabelText(/API Key Name/i);
      const longName = 'a'.repeat(101);
      await user.type(keyNameInput, longName);

      const createButton = screen.getByRole('button', { name: /Create API Key/i });
      await user.click(createButton);

      await waitFor(() => {
        expect(screen.getByText(/at most 100 characters/i)).toBeInTheDocument();
      });
    });

    it('should show error when no scopes are selected', async () => {
      const user = userEvent.setup();
      render(<ApiKeyCreationModal {...defaultProps} />);

      const keyNameInput = screen.getByLabelText(/API Key Name/i);
      await user.type(keyNameInput, 'Test Key');

      const createButton = screen.getByRole('button', { name: /Create API Key/i });
      await user.click(createButton);

      await waitFor(() => {
        expect(screen.getByText(/At least one scope is required/i)).toBeInTheDocument();
      });
    });

    it('should show error when expiration date is in the past', async () => {
      const user = userEvent.setup();
      render(<ApiKeyCreationModal {...defaultProps} />);

      const keyNameInput = screen.getByLabelText(/API Key Name/i);
      await user.type(keyNameInput, 'Test Key');

      const readCheckbox = screen.getByLabelText(/Read/i);
      await user.click(readCheckbox);

      const expiresAtInput = screen.getByLabelText(/Expiration Date/i);
      const pastDate = new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString().slice(0, 16);
      await user.type(expiresAtInput, pastDate);

      const createButton = screen.getByRole('button', { name: /Create API Key/i });
      await user.click(createButton);

      await waitFor(() => {
        expect(screen.getByText(/must be in the future/i)).toBeInTheDocument();
      });
    });

    it('should show error when metadata is invalid JSON', async () => {
      const user = userEvent.setup();
      render(<ApiKeyCreationModal {...defaultProps} />);

      const keyNameInput = screen.getByLabelText(/API Key Name/i);
      await user.type(keyNameInput, 'Test Key');

      const readCheckbox = screen.getByLabelText(/Read/i);
      await user.click(readCheckbox);

      const metadataInput = screen.getByLabelText(/Metadata/i) as HTMLTextAreaElement;
      // Use paste instead of type to avoid curly brace interpretation
      await user.click(metadataInput);
      await user.paste('{ invalid json }');

      const createButton = screen.getByRole('button', { name: /Create API Key/i });
      await user.click(createButton);

      await waitFor(() => {
        expect(screen.getByText(/must be valid JSON/i)).toBeInTheDocument();
      });
    });

    it('should clear validation errors when user corrects input', async () => {
      const user = userEvent.setup();
      render(<ApiKeyCreationModal {...defaultProps} />);

      // Trigger validation error
      const createButton = screen.getByRole('button', { name: /Create API Key/i });
      await user.click(createButton);

      await waitFor(() => {
        expect(screen.getByText(/API key name is required/i)).toBeInTheDocument();
      });

      // Correct the input
      const keyNameInput = screen.getByLabelText(/API Key Name/i);
      await user.type(keyNameInput, 'Test Key');

      await waitFor(() => {
        expect(screen.queryByText(/API key name is required/i)).not.toBeInTheDocument();
      });
    });
  });

  // ========== Form Interaction Tests ==========

  describe('Form Interactions', () => {
    it('should allow selecting and deselecting scopes', async () => {
      const user = userEvent.setup();
      render(<ApiKeyCreationModal {...defaultProps} />);

      const readCheckbox = screen.getByLabelText(/Read/i) as HTMLInputElement;
      const writeCheckbox = screen.getByLabelText(/Write/i) as HTMLInputElement;

      expect(readCheckbox.checked).toBe(false);
      expect(writeCheckbox.checked).toBe(false);

      await user.click(readCheckbox);
      expect(readCheckbox.checked).toBe(true);

      await user.click(writeCheckbox);
      expect(writeCheckbox.checked).toBe(true);

      await user.click(readCheckbox);
      expect(readCheckbox.checked).toBe(false);
    });

    it('should allow entering valid metadata JSON', async () => {
      const user = userEvent.setup();
      render(<ApiKeyCreationModal {...defaultProps} />);

      const metadataInput = screen.getByLabelText(/Metadata/i) as HTMLTextAreaElement;
      const validJson = '{"environment": "production"}';

      // Use paste instead of type to avoid curly brace interpretation
      await user.click(metadataInput);
      await user.paste(validJson);

      expect(metadataInput.value).toBe(validJson);
    });

    it('should allow selecting future expiration date', async () => {
      const user = userEvent.setup();
      render(<ApiKeyCreationModal {...defaultProps} />);

      const expiresAtInput = screen.getByLabelText(/Expiration Date/i) as HTMLInputElement;
      const futureDate = new Date(Date.now() + 90 * 24 * 60 * 60 * 1000).toISOString().slice(0, 16);

      await user.type(expiresAtInput, futureDate);

      expect(expiresAtInput.value).toBe(futureDate);
    });
  });

  // ========== Submission Tests ==========

  describe('Form Submission', () => {
    it('should call API and onApiKeyCreated on successful submission', async () => {
      const user = userEvent.setup();
      const mockApiResponse = {
        id: '550e8400-e29b-41d4-a716-446655440000',
        keyName: 'Test Key',
        keyPrefix: 'mpl_test_',
        plaintextKey: 'mpl_test_secretkey123',
        scopes: 'read,write',
        createdAt: new Date().toISOString(),
        expiresAt: null,
      };

      vi.mocked(api.auth.createApiKey).mockResolvedValue(mockApiResponse);

      render(<ApiKeyCreationModal {...defaultProps} />);

      const keyNameInput = screen.getByLabelText(/API Key Name/i);
      await user.type(keyNameInput, 'Test Key');

      const readCheckbox = screen.getByLabelText(/Read/i);
      await user.click(readCheckbox);

      const writeCheckbox = screen.getByLabelText(/Write/i);
      await user.click(writeCheckbox);

      const createButton = screen.getByRole('button', { name: /Create API Key/i });
      await user.click(createButton);

      await waitFor(() => {
        expect(api.auth.createApiKey).toHaveBeenCalledWith({
          keyName: 'Test Key',
          scopes: 'read,write',
          expiresAt: null,
          metadata: null,
        });
        expect(mockOnApiKeyCreated).toHaveBeenCalledWith(mockApiResponse);
      });
    });

    it('should show success state after API key creation', async () => {
      const user = userEvent.setup();
      const mockApiResponse = {
        id: '550e8400-e29b-41d4-a716-446655440000',
        keyName: 'Test Key',
        keyPrefix: 'mpl_test_',
        plaintextKey: 'mpl_test_secretkey123',
        scopes: 'read,write',
        createdAt: new Date().toISOString(),
        expiresAt: null,
      };

      vi.mocked(api.auth.createApiKey).mockResolvedValue(mockApiResponse);

      render(<ApiKeyCreationModal {...defaultProps} />);

      const keyNameInput = screen.getByLabelText(/API Key Name/i);
      await user.type(keyNameInput, 'Test Key');

      const readCheckbox = screen.getByLabelText(/Read/i);
      await user.click(readCheckbox);

      const createButton = screen.getByRole('button', { name: /Create API Key/i });
      await user.click(createButton);

      await waitFor(() => {
        expect(screen.getByText(/API Key Created Successfully/i)).toBeInTheDocument();
        expect(screen.getByText(/mpl_test_secretkey123/i)).toBeInTheDocument();
      });
    });

    it('should handle API errors gracefully', async () => {
      const user = userEvent.setup();
      vi.mocked(api.auth.createApiKey).mockRejectedValue(new Error('Network error'));

      render(<ApiKeyCreationModal {...defaultProps} />);

      const keyNameInput = screen.getByLabelText(/API Key Name/i);
      await user.type(keyNameInput, 'Test Key');

      const readCheckbox = screen.getByLabelText(/Read/i);
      await user.click(readCheckbox);

      const createButton = screen.getByRole('button', { name: /Create API Key/i });
      await user.click(createButton);

      await waitFor(() => {
        expect(screen.getByText(/Network error/i)).toBeInTheDocument();
      });
    });

    it('should disable form during submission', async () => {
      const user = userEvent.setup();
      vi.mocked(api.auth.createApiKey).mockImplementation(
        () => new Promise(resolve => setTimeout(resolve, 1000))
      );

      render(<ApiKeyCreationModal {...defaultProps} />);

      const keyNameInput = screen.getByLabelText(/API Key Name/i);
      await user.type(keyNameInput, 'Test Key');

      const readCheckbox = screen.getByLabelText(/Read/i);
      await user.click(readCheckbox);

      const createButton = screen.getByRole('button', { name: /Create API Key/i });
      await user.click(createButton);

      await waitFor(() => {
        expect(keyNameInput).toBeDisabled();
        expect(readCheckbox).toBeDisabled();
        expect(createButton).toBeDisabled();
      });
    });
  });

  // ========== Success State Tests ==========

  describe('Success State', () => {
    it('should display created API key details', async () => {
      const user = userEvent.setup();
      const mockApiResponse = {
        id: '550e8400-e29b-41d4-a716-446655440000',
        keyName: 'Production Key',
        keyPrefix: 'mpl_live_',
        plaintextKey: 'mpl_live_secretkey123',
        scopes: 'read,write,admin',
        createdAt: '2025-12-11T17:00:00Z',
        expiresAt: '2026-03-11T17:00:00Z',
      };

      vi.mocked(api.auth.createApiKey).mockResolvedValue(mockApiResponse);

      render(<ApiKeyCreationModal {...defaultProps} />);

      const keyNameInput = screen.getByLabelText(/API Key Name/i);
      await user.type(keyNameInput, 'Production Key');

      const readCheckbox = screen.getByLabelText(/Read/i);
      await user.click(readCheckbox);

      const createButton = screen.getByRole('button', { name: /Create API Key/i });
      await user.click(createButton);

      await waitFor(() => {
        expect(screen.getByText('Production Key')).toBeInTheDocument();
        expect(screen.getByText('mpl_live_')).toBeInTheDocument();
        expect(screen.getByText('read,write,admin')).toBeInTheDocument();
        expect(screen.getByText(/mpl_live_secretkey123/i)).toBeInTheDocument();
      });
    });

    it('should allow copying API key to clipboard', async () => {
      const user = userEvent.setup();
      const mockApiResponse = {
        id: '550e8400-e29b-41d4-a716-446655440000',
        keyName: 'Test Key',
        keyPrefix: 'mpl_test_',
        plaintextKey: 'mpl_test_secretkey123',
        scopes: 'read',
        createdAt: new Date().toISOString(),
        expiresAt: null,
      };

      vi.mocked(api.auth.createApiKey).mockResolvedValue(mockApiResponse);

      // Mock clipboard API
      Object.defineProperty(navigator, 'clipboard', {
        value: {
          writeText: vi.fn().mockResolvedValue(undefined),
        },
        writable: true,
      });

      render(<ApiKeyCreationModal {...defaultProps} />);

      const keyNameInput = screen.getByLabelText(/API Key Name/i);
      await user.type(keyNameInput, 'Test Key');

      const readCheckbox = screen.getByLabelText(/Read/i);
      await user.click(readCheckbox);

      const createButton = screen.getByRole('button', { name: /Create API Key/i });
      await user.click(createButton);

      await waitFor(() => {
        expect(screen.getByText(/API Key Created Successfully/i)).toBeInTheDocument();
      });

      const copyButton = screen.getByRole('button', { name: /Copy API key to clipboard/i });
      await user.click(copyButton);

      await waitFor(() => {
        expect(navigator.clipboard.writeText).toHaveBeenCalledWith('mpl_test_secretkey123');
        expect(screen.getByTestId('copied-text')).toBeInTheDocument();
      });
    });
  });

  // ========== Modal Behavior Tests ==========

  describe('Modal Behavior', () => {
    it('should call onClose when cancel button is clicked', async () => {
      const user = userEvent.setup();
      render(<ApiKeyCreationModal {...defaultProps} />);

      const cancelButton = screen.getByRole('button', { name: /Cancel/i });
      await user.click(cancelButton);

      expect(mockOnClose).toHaveBeenCalled();
    });

    it('should reset form state when modal closes', async () => {
      const { rerender } = render(<ApiKeyCreationModal {...defaultProps} />);

      const keyNameInput = screen.getByLabelText(/API Key Name/i) as HTMLInputElement;
      const user = userEvent.setup();
      await user.type(keyNameInput, 'Test Key');

      expect(keyNameInput.value).toBe('Test Key');

      rerender(<ApiKeyCreationModal {...defaultProps} isOpen={false} />);
      rerender(<ApiKeyCreationModal {...defaultProps} isOpen={true} />);

      const newKeyNameInput = screen.getByLabelText(/API Key Name/i) as HTMLInputElement;
      expect(newKeyNameInput.value).toBe('');
    });
  });

  // ========== Accessibility Tests ==========

  describe('Accessibility', () => {
    it('should have proper ARIA labels', () => {
      render(<ApiKeyCreationModal {...defaultProps} />);

      expect(screen.getByRole('dialog')).toHaveAttribute('aria-describedby');
      expect(screen.getByLabelText(/API Key Name/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/Expiration Date/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/Metadata/i)).toBeInTheDocument();
    });

    it('should mark required fields with aria-invalid when validation fails', async () => {
      const user = userEvent.setup();
      render(<ApiKeyCreationModal {...defaultProps} />);

      const createButton = screen.getByRole('button', { name: /Create API Key/i });
      await user.click(createButton);

      await waitFor(() => {
        const keyNameInput = screen.getByLabelText(/API Key Name/i);
        expect(keyNameInput).toHaveAttribute('aria-invalid', 'true');
      });
    });

    it('should have keyboard navigation support', async () => {
      const user = userEvent.setup();
      render(<ApiKeyCreationModal {...defaultProps} />);

      const keyNameInput = screen.getByLabelText(/API Key Name/i);
      keyNameInput.focus();

      expect(document.activeElement).toBe(keyNameInput);

      await user.tab();
      expect(document.activeElement).not.toBe(keyNameInput);
    });
  });
});
