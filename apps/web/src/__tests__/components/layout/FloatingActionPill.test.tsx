import { render, screen, fireEvent } from '@testing-library/react';
import { vi } from 'vitest';

import { FloatingActionPill } from '@/components/layout/FloatingActionPill';

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }));

beforeEach(() => mockPush.mockClear());

describe('FloatingActionPill', () => {
  it('renders desktop pill with correct label for "sessions" page', () => {
    render(<FloatingActionPill page="sessions" />);
    expect(screen.getAllByText(/🎯 Sessioni/)[0]).toBeInTheDocument();
  });

  it('renders desktop CTA for "chat" page', () => {
    render(<FloatingActionPill page="chat" />);
    const buttons = screen.getAllByText('+ Nuova chat');
    expect(buttons.length).toBeGreaterThan(0);
  });

  it('navigates to correct href when desktop CTA clicked', () => {
    render(<FloatingActionPill page="sessions" />);
    const buttons = screen.getAllByText('▶ Nuova sessione');
    fireEvent.click(buttons[0]);
    expect(mockPush).toHaveBeenCalledWith('/sessions/new');
  });

  it('does not render secondary icon buttons when no callbacks provided', () => {
    render(<FloatingActionPill page="library" />);
    expect(screen.queryByLabelText('Cerca')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Ordina')).not.toBeInTheDocument();
  });

  it('renders search button when onSearch callback provided', () => {
    const onSearch = vi.fn();
    render(<FloatingActionPill page="library" onSearch={onSearch} />);
    const searchBtns = screen.getAllByLabelText('Cerca');
    expect(searchBtns.length).toBeGreaterThan(0);
    fireEvent.click(searchBtns[0]);
    expect(onSearch).toHaveBeenCalledTimes(1);
  });
});
