/**
 * Step4Confirm Tests - Issue #4141
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { Step4Confirm } from '../Step4Confirm';

// Mock router
const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
  }),
}));

// Mock toast
vi.mock('@/components/layout', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

// Mock store
vi.mock('@/lib/stores/pdf-wizard-store', () => ({
  usePdfWizardStore: vi.fn((selector) => {
    const mockStore = {
      pdfDocumentId: 'pdf-123',
      qualityScore: 0.85,
      extractedTitle: 'Catan',
      manualFields: {
        minPlayers: 3,
        maxPlayers: 4,
        playingTime: 90,
        minAge: 10,
        description: 'Strategy game',
      },
      selectedBggId: 13,
      bggDetails: {
        id: 13,
        name: 'Catan',
        yearPublished: 1995,
        minPlayers: 3,
        maxPlayers: 4,
        playingTime: 90,
        minAge: 10,
        rating: 7.2,
        thumbnail: null,
      },
      reset: vi.fn(),
    };
    return selector ? selector(mockStore) : mockStore;
  }),
}));

describe('Step4Confirm', () => {
  const mockOnBack = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render summary title', () => {
    render(<Step4Confirm onBack={mockOnBack} userRole="Admin" />);

    expect(screen.getByText('Review & Confirm')).toBeInTheDocument();
  });

  it('should display game information summary', () => {
    render(<Step4Confirm onBack={mockOnBack} userRole="Admin" />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText(/Quality: 85%/i)).toBeInTheDocument();
    expect(screen.getByText('3-4')).toBeInTheDocument();
    expect(screen.getByText('90 min')).toBeInTheDocument();
    expect(screen.getByText('10+')).toBeInTheDocument();
  });

  it('should display BGG match when selected', () => {
    render(<Step4Confirm onBack={mockOnBack} userRole="Admin" />);

    expect(screen.getByText(/Catan \(1995\)/i)).toBeInTheDocument();
    expect(screen.getByText(/BGG ID: 13/i)).toBeInTheDocument();
  });

  // TODO: Add test for no BGG match with proper mock setup

  it('should show approval notice for Editor role', () => {
    render(<Step4Confirm onBack={mockOnBack} userRole="Editor" />);

    expect(screen.getByText(/Approval Required/i)).toBeInTheDocument();
    expect(
      screen.getByText(/This game will require Admin approval/i)
    ).toBeInTheDocument();
  });

  it('should NOT show approval notice for Admin role', () => {
    render(<Step4Confirm onBack={mockOnBack} userRole="Admin" />);

    expect(screen.queryByText(/Approval Required/i)).not.toBeInTheDocument();
  });

  it('should show correct button label for Admin', () => {
    render(<Step4Confirm onBack={mockOnBack} userRole="Admin" />);

    expect(screen.getByRole('button', { name: /Publish Game/i })).toBeInTheDocument();
  });

  it('should show correct button label for Editor', () => {
    render(<Step4Confirm onBack={mockOnBack} userRole="Editor" />);

    expect(
      screen.getByRole('button', { name: /Submit for Approval/i })
    ).toBeInTheDocument();
  });

  it('should call onBack when back button clicked', () => {
    render(<Step4Confirm onBack={mockOnBack} userRole="Admin" />);

    const backButton = screen.getByRole('button', { name: /back/i });
    fireEvent.click(backButton);

    expect(mockOnBack).toHaveBeenCalledTimes(1);
  });

  it('should display description when provided', () => {
    render(<Step4Confirm onBack={mockOnBack} userRole="Admin" />);

    expect(screen.getByText('Strategy game')).toBeInTheDocument();
  });

  // TODO: Add test for default values with proper mock setup
});
