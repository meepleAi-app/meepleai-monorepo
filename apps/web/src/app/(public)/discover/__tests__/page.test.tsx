/**
 * Discovery Page Tests (Task #9)
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, beforeEach } from 'vitest';

import DiscoverAgentsPage from '../page';

// Create query client for tests
const createTestQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

// Wrapper with QueryClientProvider
function renderWithClient(ui: React.ReactElement) {
  const queryClient = createTestQueryClient();
  return render(
    <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
  );
}

describe('DiscoverAgentsPage', () => {
  it('renders all agent cards', () => {
    renderWithClient(<DiscoverAgentsPage />);

    expect(screen.getByText('Tutor Agent')).toBeInTheDocument();
    expect(screen.getByText('Arbitro Agent')).toBeInTheDocument();
    expect(screen.getByText('Decisore Agent')).toBeInTheDocument();
  });

  it('shows agent descriptions and capabilities', () => {
    renderWithClient(<DiscoverAgentsPage />);

    // Use getAllByText since descriptions and capabilities may overlap
    const tutorials = screen.getAllByText(/Interactive tutorials/i);
    const validation = screen.getAllByText(/Move validation/i);
    expect(tutorials.length).toBeGreaterThan(0);
    expect(validation.length).toBeGreaterThan(0);
  });

  it('has "Chat with" buttons linking to chat page (Issue #11)', () => {
    renderWithClient(<DiscoverAgentsPage />);

    const tutorLink = screen.getByRole('link', { name: /Chat with Tutor Agent/i });
    expect(tutorLink).toHaveAttribute('href', '/chat?agent=tutor');

    const arbitroLink = screen.getByRole('link', { name: /Chat with Arbitro Agent/i });
    expect(arbitroLink).toHaveAttribute('href', '/chat?agent=arbitro');

    const decisoreLink = screen.getByRole('link', { name: /Chat with Decisore Agent/i });
    expect(decisoreLink).toHaveAttribute('href', '/chat?agent=decisore');
  });

  it('filters agents by search query', () => {
    renderWithClient(<DiscoverAgentsPage />);

    const searchInput = screen.getByPlaceholderText(/Search agents/i);
    fireEvent.change(searchInput, { target: { value: 'strategic' } });

    // Only Decisore should be visible
    expect(screen.queryByText('Tutor Agent')).not.toBeInTheDocument();
    expect(screen.queryByText('Arbitro Agent')).not.toBeInTheDocument();
    expect(screen.getByText('Decisore Agent')).toBeInTheDocument();
  });

  it('filters agents by type', () => {
    renderWithClient(<DiscoverAgentsPage />);

    const tutorFilterBtn = screen.getByRole('button', { name: /^Tutor$/i });
    fireEvent.click(tutorFilterBtn);

    // Only Tutor should be visible
    expect(screen.getByText('Tutor Agent')).toBeInTheDocument();
    expect(screen.queryByText('Arbitro Agent')).not.toBeInTheDocument();
    expect(screen.queryByText('Decisore Agent')).not.toBeInTheDocument();
  });

  it('shows "All" filter by default', () => {
    renderWithClient(<DiscoverAgentsPage />);

    const allBtn = screen.getByRole('button', { name: /^All$/i });
    expect(allBtn).toBeInTheDocument(); // Just check it exists and is rendered
  });

  it('displays agent capabilities list', () => {
    renderWithClient(<DiscoverAgentsPage />);

    expect(screen.getByText('Game setup help')).toBeInTheDocument();
    expect(screen.getByText('Move validation')).toBeInTheDocument();
    expect(screen.getByText('Move suggestions')).toBeInTheDocument();
  });

  it('shows no results message when search has no matches', () => {
    renderWithClient(<DiscoverAgentsPage />);

    const searchInput = screen.getByPlaceholderText(/Search agents/i);
    fireEvent.change(searchInput, { target: { value: 'xyz-nonexistent' } });

    expect(screen.getByText(/No agents found/i)).toBeInTheDocument();
  });
});
