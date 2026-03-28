import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

// Mock the BGG search hook
vi.mock('@/hooks/queries/useSearchBggGames', () => ({
  useSearchBggGames: vi.fn(() => ({
    data: undefined,
    isLoading: false,
    error: null,
  })),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

import { FullScreenSearch } from './FullScreenSearch';

describe('FullScreenSearch', () => {
  it('renders search input when open', () => {
    render(<FullScreenSearch open onClose={() => {}} onSelectGame={() => {}} />);
    expect(screen.getByPlaceholderText(/cerca un gioco/i)).toBeInTheDocument();
  });

  it('does not render when closed', () => {
    const { container } = render(
      <FullScreenSearch open={false} onClose={() => {}} onSelectGame={() => {}} />
    );
    expect(container.firstChild).toBeNull();
  });

  it('calls onClose when close button is clicked', () => {
    const onClose = vi.fn();
    render(<FullScreenSearch open onClose={onClose} onSelectGame={() => {}} />);
    fireEvent.click(screen.getByLabelText(/chiudi/i));
    expect(onClose).toHaveBeenCalledOnce();
  });
});
