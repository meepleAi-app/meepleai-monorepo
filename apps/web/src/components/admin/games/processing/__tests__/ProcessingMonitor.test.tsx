/**
 * ProcessingMonitor Component Tests
 * Issue #4673: Real-time PDF processing pipeline visualization coverage.
 *
 * Tests:
 * - Renders game title in header
 * - Shows all 7 pipeline step labels
 * - Renders in connecting state by default
 * - Marks active step correctly when progress received
 * - Shows complete card with "Test Agent" link on isComplete
 * - Shows error card with errorMessage on isFailed
 * - Shows reconnect button when connectionState is 'error'
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';

import { ProcessingMonitor } from '../ProcessingMonitor';

// ─── Mocks ───────────────────────────────────────────────────────────────────

vi.mock('@/lib/api', () => ({
  getApiBase: vi.fn(() => ''),
}));

const mockReconnect = vi.fn();

const { mockUseWizardProgressStream } = vi.hoisted(() => ({
  mockUseWizardProgressStream: vi.fn(),
}));

vi.mock('@/hooks/useWizardProgressStream', () => ({
  useWizardProgressStream: mockUseWizardProgressStream,
}));

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

// ─── Helpers ─────────────────────────────────────────────────────────────────

const defaultStreamReturn = {
  progress: null,
  connectionState: 'connecting' as const,
  isComplete: false,
  isFailed: false,
  reconnect: mockReconnect,
};

function setup(overrides = {}) {
  mockUseWizardProgressStream.mockReturnValue({
    ...defaultStreamReturn,
    ...overrides,
  });
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('ProcessingMonitor', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render game title in header', () => {
    setup();

    render(<ProcessingMonitor gameId="game-uuid" gameTitle="Gloomhaven" />);

    expect(screen.getByText(/Processing Gloomhaven/i)).toBeDefined();
  });

  it('should render all 7 pipeline step labels', () => {
    setup();

    render(<ProcessingMonitor gameId="game-uuid" />);

    const expectedSteps = ['Pending', 'Uploading', 'Extracting', 'Chunking', 'Embedding', 'Indexing', 'Ready'];
    for (const step of expectedSteps) {
      expect(screen.getByText(step)).toBeDefined();
    }
  });

  it('should show connecting message when no progress received', () => {
    setup({ progress: null, connectionState: 'connecting' });

    render(<ProcessingMonitor gameId="game-uuid" />);

    expect(screen.getByText(/Connecting to processing stream/i)).toBeDefined();
  });

  it('should show progress message when event received', () => {
    setup({
      progress: {
        currentStep: 'extracting',
        pdfState: 'Extracting',
        agentExists: false,
        overallPercent: 40,
        message: 'Extracting text from PDF...',
        isComplete: false,
        errorMessage: null,
        priority: 'Admin',
        timestamp: '2026-02-21T10:00:00Z',
      },
      connectionState: 'connected',
    });

    render(<ProcessingMonitor gameId="game-uuid" />);

    expect(screen.getByText('Extracting text from PDF...')).toBeDefined();
  });

  it('should show "Processing Complete!" card when isComplete is true', () => {
    setup({
      progress: {
        currentStep: 'ready',
        pdfState: 'Ready',
        agentExists: true,
        overallPercent: 100,
        message: 'Done!',
        isComplete: true,
        errorMessage: null,
        priority: 'Admin',
        timestamp: '2026-02-21T10:00:00Z',
      },
      isComplete: true,
      isFailed: false,
      connectionState: 'closed',
    });

    render(<ProcessingMonitor gameId="game-uuid" gameTitle="Gloomhaven" />);

    expect(screen.getByText('Processing Complete!')).toBeDefined();
    expect(screen.getByText(/The PDF has been fully processed/i)).toBeDefined();
  });

  it('should render "Test Agent" link to correct URL when complete', () => {
    setup({
      progress: {
        currentStep: 'ready',
        pdfState: 'Ready',
        agentExists: true,
        overallPercent: 100,
        message: 'Done!',
        isComplete: true,
        errorMessage: null,
        priority: 'Admin',
        timestamp: '2026-02-21T10:00:00Z',
      },
      isComplete: true,
      isFailed: false,
      connectionState: 'closed',
    });

    render(<ProcessingMonitor gameId="game-uuid-1" gameTitle="Gloomhaven" />);

    const testAgentLink = screen.getByRole('link', { name: /Test Agent/i });
    expect(testAgentLink.getAttribute('href')).toContain('game-uuid-1');
  });

  it('should show error card with errorMessage when isFailed is true', () => {
    setup({
      progress: {
        currentStep: 'failed',
        pdfState: 'Failed',
        agentExists: false,
        overallPercent: 40,
        message: 'Processing failed',
        isComplete: false,
        errorMessage: 'PDF extraction failed: invalid format',
        priority: 'Admin',
        timestamp: '2026-02-21T10:00:00Z',
      },
      isFailed: true,
      isComplete: false,
      connectionState: 'closed',
    });

    render(<ProcessingMonitor gameId="game-uuid" />);

    expect(screen.getByText('Processing Failed')).toBeDefined();
    expect(screen.getByText('PDF extraction failed: invalid format')).toBeDefined();
  });

  it('should not show error card when isFailed is false', () => {
    setup({ isFailed: false });

    render(<ProcessingMonitor gameId="game-uuid" />);

    expect(screen.queryByText('Processing Failed')).toBeNull();
  });

  it('should show reconnect button when connectionState is "error"', () => {
    setup({ connectionState: 'error' });

    render(<ProcessingMonitor gameId="game-uuid" />);

    expect(screen.getByText(/Reconnect/i)).toBeDefined();
  });

  it('should call reconnect when reconnect button is clicked', async () => {
    setup({ connectionState: 'error' });

    const user = userEvent.setup();
    render(<ProcessingMonitor gameId="game-uuid" />);

    const reconnectBtn = screen.getByRole('button', { name: /Reconnect/i });
    await user.click(reconnectBtn);

    expect(mockReconnect).toHaveBeenCalledOnce();
  });

  it('should not show reconnect button when connection is healthy', () => {
    setup({ connectionState: 'connected' });

    render(<ProcessingMonitor gameId="game-uuid" />);

    expect(screen.queryByRole('button', { name: /Reconnect/i })).toBeNull();
  });

  it('should show back link to /admin/shared-games/all', () => {
    setup();

    render(<ProcessingMonitor gameId="game-uuid" />);

    const backLink = screen.getByRole('link', { name: '' });
    expect(backLink.getAttribute('href')).toBe('/admin/shared-games/all');
  });

  it('should show "Admin Priority" badge when priority is Admin', () => {
    setup({
      progress: {
        currentStep: 'extracting',
        pdfState: 'Extracting',
        agentExists: false,
        overallPercent: 40,
        message: 'Extracting...',
        isComplete: false,
        errorMessage: null,
        priority: 'Admin',
        timestamp: '2026-02-21T10:00:00Z',
      },
      connectionState: 'connected',
    });

    render(<ProcessingMonitor gameId="game-uuid" />);

    expect(screen.getByText('Admin Priority')).toBeDefined();
  });
});
