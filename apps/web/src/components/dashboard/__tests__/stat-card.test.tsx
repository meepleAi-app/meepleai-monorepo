/**
 * StatCard Tests - Issue #4586
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { StatCard } from '../stat-card';

describe('StatCard', () => {
  it('renders icon, value, and label', () => {
    render(<StatCard icon="🎲" value={47} label="Giochi Collezione" />);

    expect(screen.getByText('🎲')).toBeInTheDocument();
    expect(screen.getByText('47')).toBeInTheDocument();
    expect(screen.getByText('Giochi Collezione')).toBeInTheDocument();
  });

  it('renders optional sublabel', () => {
    render(<StatCard icon="🎯" value={12} label="Partite" sublabel="Questo Mese" />);

    expect(screen.getByText('Questo Mese')).toBeInTheDocument();
  });

  it('handles string values', () => {
    render(<StatCard icon="⏱️" value="8h 30m" label="Giocate" />);

    expect(screen.getByText('8h 30m')).toBeInTheDocument();
  });
});
