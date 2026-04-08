import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { KpiStrip } from '../sections/KpiStrip';

describe('KpiStrip', () => {
  const kpis = {
    games: 47,
    sessions: 128,
    friends: 8,
    chats: 36,
  };

  it('renders all 4 KPI values', () => {
    render(<KpiStrip kpis={kpis} />);
    expect(screen.getByText('47')).toBeInTheDocument();
    expect(screen.getByText('128')).toBeInTheDocument();
    expect(screen.getByText('8')).toBeInTheDocument();
    expect(screen.getByText('36')).toBeInTheDocument();
  });

  it('renders all 4 KPI labels', () => {
    render(<KpiStrip kpis={kpis} />);
    expect(screen.getByText(/libreria/i)).toBeInTheDocument();
    expect(screen.getByText(/sessioni/i)).toBeInTheDocument();
    expect(screen.getByText(/amici/i)).toBeInTheDocument();
    expect(screen.getByText(/chat/i)).toBeInTheDocument();
  });
});
