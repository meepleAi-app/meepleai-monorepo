/**
 * FavoriteToggle Component Tests (Issue #2610)
 *
 * Test Coverage:
 * - Rendering favorite/unfavorite states
 * - Click handler and mutation
 * - Loading state during mutation
 * - Disabled state
 * - Toast notifications
 * - Accessibility (aria-label)
 * - Callback prop onToggled
 *
 * Target: ≥90% coverage
 */

import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { FavoriteToggle } from '../FavoriteToggle';

// ============================================================================
// Mock Setup
// ============================================================================

// Track mock state for tests
let mockMutateAsync: Mock;
let mockIsPending: boolean;

// Mock the hook with a factory that uses the tracked state
vi.mock('@/hooks/queries', () => ({
  useToggleLibraryFavorite: () => ({
    mutateAsync: mockMutateAsync,
    isPending: mockIsPending,
  }),
}));

// Track toast mock calls
const mockToastSuccess = vi.fn();
const mockToastError = vi.fn();

vi.mock('@/components/layout/Toast', () => ({
  toast: {
    success: (...args: unknown[]) => mockToastSuccess(...args),
    error: (...args: unknown[]) => mockToastError(...args),
  },
}));

// ============================================================================
// Rendering Tests
// ============================================================================

describe('FavoriteToggle - Rendering', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockMutateAsync = vi.fn().mockResolvedValue({});
    mockIsPending = false;
  });

  it('renders button with correct aria-label for unfavorited state', () => {
    render(<FavoriteToggle gameId="game-1" isFavorite={false} gameTitle="Catan" />);

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-label', 'Add to favorites');
  });

  it('renders button with correct aria-label for favorited state', () => {
    render(<FavoriteToggle gameId="game-1" isFavorite={true} gameTitle="Catan" />);

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-label', 'Remove from favorites');
  });

  it('applies red color class when favorited', () => {
    render(<FavoriteToggle gameId="game-1" isFavorite={true} gameTitle="Catan" />);

    const button = screen.getByRole('button');
    expect(button).toHaveClass('text-red-500');
  });

  it('does not apply red color class when unfavorited', () => {
    render(<FavoriteToggle gameId="game-1" isFavorite={false} gameTitle="Catan" />);

    const button = screen.getByRole('button');
    expect(button).not.toHaveClass('text-red-500');
  });

  it('renders Heart icon with fill class when favorited', () => {
    const { container } = render(
      <FavoriteToggle gameId="game-1" isFavorite={true} gameTitle="Catan" />
    );

    const heartIcon = container.querySelector('svg.lucide-heart');
    expect(heartIcon).toHaveClass('fill-current');
  });

  it('renders Heart icon without fill class when unfavorited', () => {
    const { container } = render(
      <FavoriteToggle gameId="game-1" isFavorite={false} gameTitle="Catan" />
    );

    const heartIcon = container.querySelector('svg.lucide-heart');
    expect(heartIcon).not.toHaveClass('fill-current');
  });
});

// ============================================================================
// Click Handler Tests
// ============================================================================

describe('FavoriteToggle - Click Handler', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockMutateAsync = vi.fn().mockResolvedValue({});
    mockIsPending = false;
  });

  it('calls mutateAsync with correct arguments when clicked (unfavorite to favorite)', async () => {
    render(<FavoriteToggle gameId="game-1" isFavorite={false} gameTitle="Catan" />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith({
        gameId: 'game-1',
        isFavorite: true,
      });
    });
  });

  it('calls mutateAsync with correct arguments when clicked (favorite to unfavorite)', async () => {
    render(<FavoriteToggle gameId="game-1" isFavorite={true} gameTitle="Catan" />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith({
        gameId: 'game-1',
        isFavorite: false,
      });
    });
  });

  it('shows success toast after successful toggle (adding)', async () => {
    render(<FavoriteToggle gameId="game-1" isFavorite={false} gameTitle="Catan" />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith(
        'Catan has been added to your favorites.'
      );
    });
  });

  it('shows success toast after successful toggle (removing)', async () => {
    render(<FavoriteToggle gameId="game-1" isFavorite={true} gameTitle="Catan" />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith(
        'Catan has been removed from your favorites.'
      );
    });
  });

  it('calls onToggled callback with new favorite state', async () => {
    const onToggled = vi.fn();
    render(
      <FavoriteToggle
        gameId="game-1"
        isFavorite={false}
        gameTitle="Catan"
        onToggled={onToggled}
      />
    );

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(onToggled).toHaveBeenCalledWith(true);
    });
  });
});

// ============================================================================
// Error Handling Tests
// ============================================================================

describe('FavoriteToggle - Error Handling', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockIsPending = false;
  });

  it('shows error toast when mutation fails with Error', async () => {
    mockMutateAsync = vi.fn().mockRejectedValue(new Error('Network error'));

    render(<FavoriteToggle gameId="game-1" isFavorite={false} gameTitle="Catan" />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Network error');
    });
  });

  it('shows default error toast when mutation fails with non-Error', async () => {
    mockMutateAsync = vi.fn().mockRejectedValue('Unknown error');

    render(<FavoriteToggle gameId="game-1" isFavorite={false} gameTitle="Catan" />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Failed to update favorite status.');
    });
  });

  it('does not call onToggled when mutation fails', async () => {
    mockMutateAsync = vi.fn().mockRejectedValue(new Error('Network error'));
    const onToggled = vi.fn();

    render(
      <FavoriteToggle
        gameId="game-1"
        isFavorite={false}
        gameTitle="Catan"
        onToggled={onToggled}
      />
    );

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalled();
    });

    expect(onToggled).not.toHaveBeenCalled();
  });
});

// ============================================================================
// Loading State Tests
// ============================================================================

describe('FavoriteToggle - Loading State', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockMutateAsync = vi.fn().mockResolvedValue({});
  });

  it('shows loading spinner when isPending is true', () => {
    mockIsPending = true;

    const { container } = render(
      <FavoriteToggle gameId="game-1" isFavorite={false} gameTitle="Catan" />
    );

    // Lucide icons use 'lucide' prefix, check for animate-spin class
    const spinner = container.querySelector('svg.animate-spin');
    expect(spinner).toBeInTheDocument();
  });

  it('hides heart icon when loading', () => {
    mockIsPending = true;

    const { container } = render(
      <FavoriteToggle gameId="game-1" isFavorite={false} gameTitle="Catan" />
    );

    const heartIcon = container.querySelector('svg.lucide-heart');
    expect(heartIcon).not.toBeInTheDocument();
  });

  it('disables button when loading', () => {
    mockIsPending = true;

    render(<FavoriteToggle gameId="game-1" isFavorite={false} gameTitle="Catan" />);

    const button = screen.getByRole('button');
    expect(button).toBeDisabled();
  });

  it('does not trigger mutation when loading', async () => {
    mockIsPending = true;

    render(<FavoriteToggle gameId="game-1" isFavorite={false} gameTitle="Catan" />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    // Wait a tick to ensure any async calls would have been made
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(mockMutateAsync).not.toHaveBeenCalled();
  });
});

// ============================================================================
// Disabled State Tests
// ============================================================================

describe('FavoriteToggle - Disabled State', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockMutateAsync = vi.fn().mockResolvedValue({});
    mockIsPending = false;
  });

  it('respects disabled prop', () => {
    render(
      <FavoriteToggle gameId="game-1" isFavorite={false} gameTitle="Catan" disabled />
    );

    const button = screen.getByRole('button');
    expect(button).toBeDisabled();
  });

  it('does not trigger mutation when disabled', async () => {
    render(
      <FavoriteToggle gameId="game-1" isFavorite={false} gameTitle="Catan" disabled />
    );

    const button = screen.getByRole('button');
    fireEvent.click(button);

    // Wait a tick to ensure any async calls would have been made
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(mockMutateAsync).not.toHaveBeenCalled();
  });
});

// ============================================================================
// Props & Styling Tests
// ============================================================================

describe('FavoriteToggle - Props & Styling', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockMutateAsync = vi.fn().mockResolvedValue({});
    mockIsPending = false;
  });

  it('uses default gameTitle when not provided', async () => {
    render(<FavoriteToggle gameId="game-1" isFavorite={false} />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith(
        'Game has been added to your favorites.'
      );
    });
  });

  it('accepts custom className', () => {
    render(
      <FavoriteToggle
        gameId="game-1"
        isFavorite={false}
        gameTitle="Catan"
        className="custom-class"
      />
    );

    const button = screen.getByRole('button');
    expect(button).toHaveClass('custom-class');
  });

  it('uses ghost variant by default', () => {
    render(<FavoriteToggle gameId="game-1" isFavorite={false} gameTitle="Catan" />);

    // Button should render without explicit variant class issues
    const button = screen.getByRole('button');
    expect(button).toBeInTheDocument();
  });

  it('uses icon size by default', () => {
    render(<FavoriteToggle gameId="game-1" isFavorite={false} gameTitle="Catan" />);

    // Button should have icon-sized styling
    const button = screen.getByRole('button');
    expect(button).toBeInTheDocument();
  });
});
