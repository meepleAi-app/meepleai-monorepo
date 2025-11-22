import React from 'react';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { useRouter } from 'next/router';
import PromptVersionDetail from '../../pages/admin/prompts/[id]/versions/[versionId]';
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
    get: jest.fn(),
    post: jest.fn(),
  },
}));

jest.mock('../../components/PromptEditor', () => {
  return function MockPromptEditor({ value, readonly }: any) {
    return (
      <textarea
        data-testid="prompt-editor"
        value={value}
        readOnly={readonly}
      />
    );
  };
});

const mockPush = jest.fn();
const mockRouter = {
  push: mockPush,
  query: { id: 'template-123', versionId: 'version-456' },
};

const mockVersion = {
  id: 'version-456',
  templateId: 'template-123',
  versionNumber: 2,
  content: 'You are a helpful assistant',
  metadata: {
    temperature: 0.7,
    max_tokens: 1000,
  },
  isActive: false,
  createdById: 'user-1',
  createdByEmail: 'user@example.com',
  createdAt: '2024-01-15T10:30:00Z',
};

const mockActiveVersion = {
  ...mockVersion,
  isActive: true,
};

describe('PromptVersionDetail Page', () => {
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

  describe('Loading State', () => {
    it('should show loading indicator while fetching', () => {
      (api.get as jest.Mock).mockImplementation(() => new Promise(() => {}));

      render(<PromptVersionDetail />);

      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });
  });

  describe('Error Handling', () => {
    it('should display error message when fetch fails', async () => {
      (api.get as jest.Mock).mockRejectedValue(new Error('Network error'));

      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.getByText('Network error')).toBeInTheDocument();
      });

      expect(screen.getByText('Back to Template')).toBeInTheDocument();
    });

    it('should display unauthorized error when API returns null', async () => {
      (api.get as jest.Mock).mockResolvedValue(null);

      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.getByText('Unauthorized')).toBeInTheDocument();
      });
    });

    it('should display not found message when version is missing', async () => {
      (api.get as jest.Mock).mockResolvedValue(null);

      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.getByText(/Unauthorized/)).toBeInTheDocument();
      });
    });

    it('should have working back button in error state', async () => {
      (api.get as jest.Mock).mockRejectedValue(new Error('Not found'));

      render(<PromptVersionDetail />);

      await waitFor(() => {
        const backButton = screen.getByText('Back to Template');
        expect(backButton.closest('a')).toHaveAttribute(
          'href',
          '/admin/prompts/template-123'
        );
      });
    });
  });

  describe('Successful Load', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue(mockVersion);
    });

    it('should display version information', async () => {
      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.getByText('Version 2')).toBeInTheDocument();
        expect(screen.getByText(/user@example.com/)).toBeInTheDocument();
        // Date is formatted with toLocaleString()
        const formattedDate = new Date(mockVersion.createdAt).toLocaleString();
        expect(screen.getByText(new RegExp(formattedDate.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')))).toBeInTheDocument();
      });
    });

    it('should display prompt content in readonly editor', async () => {
      render(<PromptVersionDetail />);

      await waitFor(() => {
        const editor = screen.getByTestId('prompt-editor');
        expect(editor).toHaveValue('You are a helpful assistant');
        expect(editor).toHaveAttribute('readOnly');
      });
    });

    it('should display metadata when present', async () => {
      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.getByText('Metadata')).toBeInTheDocument();
        const metadataContent = screen.getByText(/"temperature": 0.7/);
        expect(metadataContent).toBeInTheDocument();
      });
    });

    it('should not display metadata section when metadata is empty', async () => {
      (api.get as jest.Mock).mockResolvedValue({
        ...mockVersion,
        metadata: {},
      });

      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.queryByText('Metadata')).not.toBeInTheDocument();
      });
    });

    it('should not display metadata section when metadata is null', async () => {
      (api.get as jest.Mock).mockResolvedValue({
        ...mockVersion,
        metadata: null,
      });

      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.queryByText('Metadata')).not.toBeInTheDocument();
      });
    });
  });

  describe('Active Version Badge', () => {
    it('should show active badge for active version', async () => {
      (api.get as jest.Mock).mockResolvedValue(mockActiveVersion);

      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.getByText('✓ Active')).toBeInTheDocument();
      });
    });

    it('should not show active badge for inactive version', async () => {
      (api.get as jest.Mock).mockResolvedValue(mockVersion);

      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.queryByText('✓ Active')).not.toBeInTheDocument();
      });
    });
  });

  describe('Activate Button', () => {
    it('should show activate button for inactive version', async () => {
      (api.get as jest.Mock).mockResolvedValue(mockVersion);

      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.getByText('Activate Version')).toBeInTheDocument();
      });
    });

    it('should not show activate button for active version', async () => {
      (api.get as jest.Mock).mockResolvedValue(mockActiveVersion);

      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.queryByText('Activate Version')).not.toBeInTheDocument();
      });
    });

    it('should activate version successfully', async () => {
      (api.get as jest.Mock).mockResolvedValue(mockVersion);
      (api.post as jest.Mock).mockResolvedValue({});

      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.getByText('Activate Version')).toBeInTheDocument();
      });

      const activateButton = screen.getByText('Activate Version');
      fireEvent.click(activateButton);

      await waitFor(() => {
        expect(api.post).toHaveBeenCalledWith(
          '/api/v1/admin/prompts/template-123/versions/version-456/activate',
          {}
        );
      });

      await waitFor(() => {
        expect(
          screen.getByText('Version activated successfully')
        ).toBeInTheDocument();
      });
    });

    it('should handle activation error', async () => {
      (api.get as jest.Mock).mockResolvedValue(mockVersion);
      (api.post as jest.Mock).mockRejectedValue(new Error('Activation failed'));

      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.getByText('Activate Version')).toBeInTheDocument();
      });

      const activateButton = screen.getByText('Activate Version');
      fireEvent.click(activateButton);

      await waitFor(() => {
        expect(screen.getByText('Activation failed')).toBeInTheDocument();
      });
    });

    it('should refetch version data after successful activation', async () => {
      (api.get as jest.Mock).mockResolvedValue(mockVersion);
      (api.post as jest.Mock).mockResolvedValue({});

      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.getByText('Activate Version')).toBeInTheDocument();
      });

      const initialCallCount = (api.get as jest.Mock).mock.calls.length;

      const activateButton = screen.getByText('Activate Version');
      fireEvent.click(activateButton);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledTimes(initialCallCount + 1);
      });
    });
  });

  describe('Navigation Links', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue(mockVersion);
    });

    it('should have back to template link', async () => {
      render(<PromptVersionDetail />);

      await waitFor(() => {
        const backButton = screen.getByText('← Back to Template');
        expect(backButton.closest('a')).toHaveAttribute(
          'href',
          '/admin/prompts/template-123'
        );
      });
    });

    it('should have compare link with version query param', async () => {
      render(<PromptVersionDetail />);

      await waitFor(() => {
        const compareButton = screen.getByText('Compare');
        expect(compareButton.closest('a')).toHaveAttribute(
          'href',
          '/admin/prompts/template-123/compare?versions=version-456'
        );
      });
    });
  });

  describe('Toast Notifications', () => {
    it('should auto-hide success toast after 5 seconds', async () => {
      (api.get as jest.Mock).mockResolvedValue(mockVersion);
      (api.post as jest.Mock).mockResolvedValue({});

      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.getByText('Activate Version')).toBeInTheDocument();
      });

      const activateButton = screen.getByText('Activate Version');
      fireEvent.click(activateButton);

      await waitFor(() => {
        expect(
          screen.getByText('Version activated successfully')
        ).toBeInTheDocument();
      });

      act(() => {
        jest.advanceTimersByTime(5000);
      });

      await waitFor(() => {
        expect(
          screen.queryByText('Version activated successfully')
        ).not.toBeInTheDocument();
      });
    });

    it('should auto-hide error toast after 5 seconds', async () => {
      (api.get as jest.Mock).mockResolvedValue(mockVersion);
      (api.post as jest.Mock).mockRejectedValue(new Error('Error'));

      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.getByText('Activate Version')).toBeInTheDocument();
      });

      const activateButton = screen.getByText('Activate Version');
      fireEvent.click(activateButton);

      await waitFor(() => {
        expect(screen.getByText('Error')).toBeInTheDocument();
      });

      act(() => {
        jest.advanceTimersByTime(5000);
      });

      await waitFor(() => {
        expect(screen.queryByText('Error')).not.toBeInTheDocument();
      });
    });
  });

  describe('Edge Cases', () => {
    it('should handle missing router query parameters', () => {
      (useRouter as jest.Mock).mockReturnValue({
        ...mockRouter,
        query: {},
      });

      render(<PromptVersionDetail />);

      // Should show loading state
      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });

    it('should not fetch when id is missing', () => {
      (useRouter as jest.Mock).mockReturnValue({
        ...mockRouter,
        query: { versionId: 'version-456' },
      });

      render(<PromptVersionDetail />);

      expect(api.get).not.toHaveBeenCalled();
    });

    it('should not fetch when versionId is missing', () => {
      (useRouter as jest.Mock).mockReturnValue({
        ...mockRouter,
        query: { id: 'template-123' },
      });

      render(<PromptVersionDetail />);

      expect(api.get).not.toHaveBeenCalled();
    });

    it('should display error toast on initial load failure', async () => {
      (api.get as jest.Mock).mockRejectedValue(new Error('Load error'));

      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.getByText('Load error')).toBeInTheDocument();
      });
    });

    it('should format metadata as pretty JSON', async () => {
      (api.get as jest.Mock).mockResolvedValue(mockVersion);

      render(<PromptVersionDetail />);

      await waitFor(() => {
        // Check that JSON metadata is rendered
        expect(screen.getByText(/"temperature":/)).toBeInTheDocument();
        expect(screen.getByText(/0.7/)).toBeInTheDocument();
        expect(screen.getByText(/"max_tokens":/)).toBeInTheDocument();
      });
    });

    it('should handle version without created by email', async () => {
      (api.get as jest.Mock).mockResolvedValue({
        ...mockVersion,
        createdByEmail: '',
      });

      render(<PromptVersionDetail />);

      await waitFor(() => {
        expect(screen.getByText('Version 2')).toBeInTheDocument();
      });
    });
  });

  describe('Date Formatting', () => {
    it('should format creation date correctly', async () => {
      (api.get as jest.Mock).mockResolvedValue(mockVersion);

      render(<PromptVersionDetail />);

      await waitFor(() => {
        const formattedDate = new Date(mockVersion.createdAt).toLocaleString();
        expect(screen.getByText(new RegExp(formattedDate))).toBeInTheDocument();
      });
    });
  });
});
