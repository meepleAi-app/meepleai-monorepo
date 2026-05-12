import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { DashboardStatCard } from '@/components/dashboard/DashboardStatCard';

describe('DashboardStatCard', () => {
  it('renders value, label, and entity data attribute', () => {
    render(<DashboardStatCard entity="game" value={42} label="Giochi" href="/library" />);
    expect(screen.getByText('42')).toBeInTheDocument();
    expect(screen.getByText('Giochi')).toBeInTheDocument();
    const link = screen.getByRole('link');
    expect(link).toHaveAttribute('data-entity', 'game');
    expect(link).toHaveAttribute('href', '/library');
  });

  it('aria-label has count + destination format', () => {
    render(
      <DashboardStatCard entity="session" value={3} label="Sessioni" href="/sessions" />
    );
    expect(
      screen.getByLabelText('Sessioni: 3 elementi. Vai a /sessions')
    ).toBeInTheDocument();
  });

  it('renders skeleton placeholder when isLoading=true', () => {
    render(
      <DashboardStatCard
        entity="agent"
        value={0}
        label="Agenti"
        href="/agents"
        isLoading
      />
    );
    expect(screen.queryByText('0')).not.toBeInTheDocument();
    expect(screen.getByTestId('stat-card-skeleton')).toBeInTheDocument();
  });

  it('renders "—" and retry button when isError=true', () => {
    const onRetry = vi.fn();
    render(
      <DashboardStatCard
        entity="event"
        value={0}
        label="Eventi"
        href="/game-nights"
        isError
        onRetry={onRetry}
      />
    );
    expect(screen.getByText('—')).toBeInTheDocument();
    const retry = screen.getByRole('button', {
      name: /Errore caricamento Eventi\. Premi Invio per riprovare/i,
    });
    expect(retry).toBeInTheDocument();
    fireEvent.click(retry);
    expect(onRetry).toHaveBeenCalledOnce();
  });

  it('retry button is disabled when isFetching=true', () => {
    const onRetry = vi.fn();
    render(
      <DashboardStatCard
        entity="event"
        value={0}
        label="Eventi"
        href="/game-nights"
        isError
        isFetching
        onRetry={onRetry}
      />
    );
    const retry = screen.getByRole('button');
    expect(retry).toBeDisabled();
    fireEvent.click(retry);
    expect(onRetry).not.toHaveBeenCalled();
  });

  it('value uses tabular-nums utility', () => {
    const { container } = render(
      <DashboardStatCard entity="game" value={42} label="x" href="/x" />
    );
    expect(container.querySelector('.tabular-nums')).toBeInTheDocument();
  });
});
