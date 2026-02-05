/**
 * Unit tests for ExportSession component (Issue #3347)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ExportSession } from '../ExportSession';

// Mock framer-motion
vi.mock('framer-motion', async () => {
  const actual = await vi.importActual('framer-motion');
  return {
    ...actual,
    motion: {
      div: ({ children, ...props }: { children?: React.ReactNode }) => <div {...props}>{children}</div>,
    },
    AnimatePresence: ({ children }: { children?: React.ReactNode }) => <>{children}</>,
  };
});

// Mock fetch
const mockFetch = vi.fn();
global.fetch = mockFetch;

// Mock clipboard
const mockWriteText = vi.fn();
Object.assign(navigator, {
  clipboard: { writeText: mockWriteText },
});

describe('ExportSession', () => {
  const defaultProps = {
    sessionId: 'session-123',
    sessionCode: 'ABC123',
    gameName: 'Catan',
    isFinalized: true,
  };

  beforeEach(() => {
    vi.clearAllMocks();
    mockFetch.mockReset();
    mockWriteText.mockReset();
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('renders export and share buttons', () => {
    render(<ExportSession {...defaultProps} />);

    expect(screen.getByRole('button', { name: /export/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /share/i })).toBeInTheDocument();
  });

  it('opens export dialog when Export button is clicked', async () => {
    const user = userEvent.setup();
    render(<ExportSession {...defaultProps} />);

    await user.click(screen.getByRole('button', { name: /export/i }));

    expect(screen.getByText('Export Session')).toBeInTheDocument();
    expect(screen.getByText('Download a PDF report of this session')).toBeInTheDocument();
  });

  it('shows export options in dialog', async () => {
    const user = userEvent.setup();
    render(<ExportSession {...defaultProps} />);

    await user.click(screen.getByRole('button', { name: /export/i }));

    expect(screen.getByText('Include score progression chart')).toBeInTheDocument();
    expect(screen.getByText('Include dice roll history')).toBeInTheDocument();
    expect(screen.getByText('Include card game history')).toBeInTheDocument();
  });

  it('toggles export options', async () => {
    const user = userEvent.setup();
    render(<ExportSession {...defaultProps} />);

    await user.click(screen.getByRole('button', { name: /export/i }));

    const diceHistoryCheckbox = screen.getByRole('checkbox', { name: /include dice roll history/i });
    expect(diceHistoryCheckbox).not.toBeChecked();

    await user.click(diceHistoryCheckbox);
    expect(diceHistoryCheckbox).toBeChecked();
  });

  it('opens share dropdown when Share button is clicked', async () => {
    const user = userEvent.setup();
    render(<ExportSession {...defaultProps} />);

    await user.click(screen.getByRole('button', { name: /share/i }));

    expect(screen.getByText('Generate Share Link')).toBeInTheDocument();
  });

  it('generates share link when clicked', async () => {
    const user = userEvent.setup();
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () =>
        Promise.resolve({
          shareUrl: 'https://meepleai.com/sessions/share/session-123',
          ogMetadata: {
            title: 'MeepleAI Session',
            description: 'Game session',
            url: 'https://meepleai.com/sessions/share/session-123',
            type: 'website',
          },
        }),
    });

    render(<ExportSession {...defaultProps} />);

    await user.click(screen.getByRole('button', { name: /share/i }));
    await user.click(screen.getByText('Generate Share Link'));

    await waitFor(() => {
      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining(`/api/v1/game-sessions/${defaultProps.sessionId}/share-link`),
        expect.objectContaining({ method: 'POST' })
      );
    });
  });

  it('shows social share options', async () => {
    const user = userEvent.setup();
    render(<ExportSession {...defaultProps} />);

    await user.click(screen.getByRole('button', { name: /share/i }));

    expect(screen.getByText('Share on Twitter')).toBeInTheDocument();
    expect(screen.getByText('Share on Facebook')).toBeInTheDocument();
  });

  it('disables social share when no link generated', async () => {
    const user = userEvent.setup();
    render(<ExportSession {...defaultProps} />);

    await user.click(screen.getByRole('button', { name: /share/i }));

    const twitterButton = screen.getByText('Share on Twitter').closest('[role="menuitem"]');
    expect(twitterButton).toHaveAttribute('data-disabled');
  });

  it('calls fetch with correct endpoint when downloading PDF', async () => {
    const user = userEvent.setup();
    const mockBlob = new Blob(['PDF content'], { type: 'application/pdf' });

    mockFetch.mockResolvedValueOnce({
      ok: true,
      headers: new Headers({
        'Content-Disposition': 'attachment; filename="session_ABC123.pdf"',
      }),
      blob: () => Promise.resolve(mockBlob),
    });

    // Mock URL methods
    const originalCreateObjectURL = URL.createObjectURL;
    const originalRevokeObjectURL = URL.revokeObjectURL;
    URL.createObjectURL = vi.fn(() => 'blob:test');
    URL.revokeObjectURL = vi.fn();

    render(<ExportSession {...defaultProps} />);

    await user.click(screen.getByRole('button', { name: /export/i }));

    // Wait for dialog to open
    await waitFor(() => {
      expect(screen.getByText('Download PDF')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /download pdf/i }));

    await waitFor(() => {
      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining(`/api/v1/game-sessions/${defaultProps.sessionId}/export/pdf`),
        expect.any(Object)
      );
    });

    // Restore URL methods
    URL.createObjectURL = originalCreateObjectURL;
    URL.revokeObjectURL = originalRevokeObjectURL;
  });

  it('shows Copy Link after generating share link', async () => {
    const user = userEvent.setup();

    // Mock the share link generation
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () =>
        Promise.resolve({
          shareUrl: 'https://meepleai.com/sessions/share/session-123',
          ogMetadata: {
            title: 'MeepleAI Session',
            description: 'Game session',
            url: 'https://meepleai.com/sessions/share/session-123',
            type: 'website',
          },
        }),
    });

    render(<ExportSession {...defaultProps} />);

    // Open dropdown and generate link
    await user.click(screen.getByRole('button', { name: /share/i }));
    await user.click(screen.getByText('Generate Share Link'));

    // Wait for fetch to complete
    await waitFor(() => {
      expect(mockFetch).toHaveBeenCalled();
    });

    // Reopen the dropdown - after generating a link, Copy Link should be available
    await user.click(screen.getByRole('button', { name: /share/i }));

    // Now Copy Link should be visible instead of Generate Share Link
    await waitFor(() => {
      expect(screen.getByText('Copy Link')).toBeInTheDocument();
    });
  });
});
