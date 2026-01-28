/**
 * Tests for TwoFactorRecoveryCodes component
 * Comprehensive coverage of recovery codes display UI (Issue #3077)
 */

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithIntl } from '../../../__tests__/fixtures/common-fixtures';
import { TwoFactorRecoveryCodes } from '../TwoFactorRecoveryCodes';

describe('TwoFactorRecoveryCodes', () => {
  const mockBackupCodes = [
    '12345678',
    '23456789',
    '34567890',
    '45678901',
    '56789012',
    '67890123',
    '78901234',
    '89012345',
  ];

  const defaultProps = {
    backupCodes: mockBackupCodes,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders default title', () => {
      renderWithIntl(<TwoFactorRecoveryCodes {...defaultProps} />);

      expect(screen.getByText('Save Your Backup Codes')).toBeInTheDocument();
    });

    it('renders custom title when provided', () => {
      renderWithIntl(
        <TwoFactorRecoveryCodes {...defaultProps} title="Your Recovery Codes" />
      );

      expect(screen.getByText('Your Recovery Codes')).toBeInTheDocument();
    });

    it('renders warning message', () => {
      renderWithIntl(<TwoFactorRecoveryCodes {...defaultProps} />);

      expect(screen.getByTestId('backup-codes-warning')).toBeInTheDocument();
      expect(screen.getByText(/important/i)).toBeInTheDocument();
    });

    it('renders all backup codes in grid', () => {
      renderWithIntl(<TwoFactorRecoveryCodes {...defaultProps} />);

      const grid = screen.getByTestId('backup-codes-grid');
      expect(grid).toBeInTheDocument();

      mockBackupCodes.forEach((code, index) => {
        expect(screen.getByTestId(`backup-code-${index}`)).toHaveTextContent(code);
      });
    });

    it('renders copy button', () => {
      renderWithIntl(<TwoFactorRecoveryCodes {...defaultProps} />);

      expect(screen.getByTestId('copy-codes-button')).toBeInTheDocument();
      expect(screen.getByText(/copy codes/i)).toBeInTheDocument();
    });

    it('renders download button', () => {
      renderWithIntl(<TwoFactorRecoveryCodes {...defaultProps} />);

      expect(screen.getByTestId('download-codes-button')).toBeInTheDocument();
      expect(screen.getByText(/download/i)).toBeInTheDocument();
    });

    it('renders acknowledgment button by default when onContinue is provided', () => {
      const onContinue = vi.fn();
      renderWithIntl(
        <TwoFactorRecoveryCodes {...defaultProps} onContinue={onContinue} />
      );

      expect(screen.getByTestId('acknowledge-codes-button')).toBeInTheDocument();
      expect(screen.getByText(/saved my codes/i)).toBeInTheDocument();
    });

    it('hides acknowledgment button when showAcknowledgment is false', () => {
      const onContinue = vi.fn();
      renderWithIntl(
        <TwoFactorRecoveryCodes
          {...defaultProps}
          onContinue={onContinue}
          showAcknowledgment={false}
        />
      );

      expect(screen.queryByTestId('acknowledge-codes-button')).not.toBeInTheDocument();
    });

    it('renders custom acknowledgment text', () => {
      const onContinue = vi.fn();
      renderWithIntl(
        <TwoFactorRecoveryCodes
          {...defaultProps}
          onContinue={onContinue}
          acknowledgmentText="Continue to Dashboard"
        />
      );

      expect(screen.getByText('Continue to Dashboard')).toBeInTheDocument();
    });
  });

  describe('User Interactions', () => {
    it('copies codes to clipboard when copy button is clicked', async () => {
      const user = userEvent.setup();

      // Mock clipboard API
      const mockClipboard = {
        writeText: vi.fn().mockResolvedValue(undefined),
      };
      Object.assign(navigator, { clipboard: mockClipboard });

      renderWithIntl(<TwoFactorRecoveryCodes {...defaultProps} />);

      const copyButton = screen.getByTestId('copy-codes-button');
      await user.click(copyButton);

      await waitFor(() => {
        expect(mockClipboard.writeText).toHaveBeenCalledWith(
          mockBackupCodes.join('\n')
        );
      });
    });

    it('shows copied feedback after copying', async () => {
      const user = userEvent.setup();

      // Mock clipboard API
      Object.assign(navigator, {
        clipboard: {
          writeText: vi.fn().mockResolvedValue(undefined),
        },
      });

      renderWithIntl(<TwoFactorRecoveryCodes {...defaultProps} />);

      const copyButton = screen.getByTestId('copy-codes-button');
      await user.click(copyButton);

      await waitFor(() => {
        expect(screen.getByText(/copied/i)).toBeInTheDocument();
      });
    });

    it('downloads codes as text file when download button is clicked', async () => {
      const user = userEvent.setup();

      // Mock URL and anchor element
      const mockCreateObjectURL = vi.fn().mockReturnValue('blob:test');
      const mockRevokeObjectURL = vi.fn();
      global.URL.createObjectURL = mockCreateObjectURL;
      global.URL.revokeObjectURL = mockRevokeObjectURL;

      const mockClick = vi.fn();
      const mockAnchor = {
        click: mockClick,
        href: '',
        download: '',
      };
      vi.spyOn(document, 'createElement').mockImplementation((tag) => {
        if (tag === 'a') return mockAnchor as any;
        return document.createElement(tag);
      });

      renderWithIntl(<TwoFactorRecoveryCodes {...defaultProps} />);

      const downloadButton = screen.getByTestId('download-codes-button');
      await user.click(downloadButton);

      expect(mockCreateObjectURL).toHaveBeenCalled();
      expect(mockClick).toHaveBeenCalled();
      expect(mockAnchor.download).toBe('meepleai-backup-codes.txt');
    });

    it('shows downloaded feedback after downloading', async () => {
      const user = userEvent.setup();

      // Mock URL methods
      global.URL.createObjectURL = vi.fn().mockReturnValue('blob:test');
      global.URL.revokeObjectURL = vi.fn();

      renderWithIntl(<TwoFactorRecoveryCodes {...defaultProps} />);

      const downloadButton = screen.getByTestId('download-codes-button');
      await user.click(downloadButton);

      await waitFor(() => {
        expect(screen.getByText(/downloaded/i)).toBeInTheDocument();
      });
    });

    it('calls onContinue when acknowledgment button is clicked', async () => {
      const user = userEvent.setup();
      const onContinue = vi.fn();

      renderWithIntl(
        <TwoFactorRecoveryCodes {...defaultProps} onContinue={onContinue} />
      );

      const acknowledgeButton = screen.getByTestId('acknowledge-codes-button');
      await user.click(acknowledgeButton);

      expect(onContinue).toHaveBeenCalledTimes(1);
    });
  });

  describe('Accessibility', () => {
    it('renders codes grid as a list', () => {
      renderWithIntl(<TwoFactorRecoveryCodes {...defaultProps} />);

      const grid = screen.getByTestId('backup-codes-grid');
      expect(grid).toHaveAttribute('role', 'list');
    });

    it('renders each code as a list item', () => {
      renderWithIntl(<TwoFactorRecoveryCodes {...defaultProps} />);

      mockBackupCodes.forEach((_, index) => {
        const codeItem = screen.getByTestId(`backup-code-${index}`);
        expect(codeItem).toHaveAttribute('role', 'listitem');
      });
    });

    it('has accessible label for codes list', () => {
      renderWithIntl(<TwoFactorRecoveryCodes {...defaultProps} />);

      const grid = screen.getByTestId('backup-codes-grid');
      expect(grid).toHaveAttribute('aria-label', expect.stringContaining('code'));
    });

    it('uses monospace font for codes', () => {
      renderWithIntl(<TwoFactorRecoveryCodes {...defaultProps} />);

      const codeItem = screen.getByTestId('backup-code-0');
      expect(codeItem).toHaveClass('font-mono');
    });

    it('makes codes selectable', () => {
      renderWithIntl(<TwoFactorRecoveryCodes {...defaultProps} />);

      const codeItem = screen.getByTestId('backup-code-0');
      expect(codeItem).toHaveClass('select-all');
    });
  });

  describe('Edge Cases', () => {
    it('handles empty backup codes array', () => {
      renderWithIntl(<TwoFactorRecoveryCodes backupCodes={[]} />);

      const grid = screen.getByTestId('backup-codes-grid');
      expect(grid.children).toHaveLength(0);
    });

    it('handles single backup code', () => {
      renderWithIntl(<TwoFactorRecoveryCodes backupCodes={['12345678']} />);

      expect(screen.getByTestId('backup-code-0')).toHaveTextContent('12345678');
    });

    it('handles clipboard API failure gracefully', async () => {
      const user = userEvent.setup();

      // Mock clipboard API to fail
      Object.assign(navigator, {
        clipboard: {
          writeText: vi.fn().mockRejectedValue(new Error('Clipboard error')),
        },
      });

      renderWithIntl(<TwoFactorRecoveryCodes {...defaultProps} />);

      const copyButton = screen.getByTestId('copy-codes-button');

      // Should not throw
      await expect(user.click(copyButton)).resolves.not.toThrow();
    });

    it('handles very long backup codes', () => {
      const longCodes = ['ABCD1234EFGH5678', 'IJKL9012MNOP3456'];

      renderWithIntl(<TwoFactorRecoveryCodes backupCodes={longCodes} />);

      expect(screen.getByTestId('backup-code-0')).toHaveTextContent(longCodes[0]);
      expect(screen.getByTestId('backup-code-1')).toHaveTextContent(longCodes[1]);
    });
  });
});
