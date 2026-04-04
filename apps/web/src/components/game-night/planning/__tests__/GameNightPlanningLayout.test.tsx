import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import { useGameNightStore } from '@/stores/game-night';
import { GameNightPlanningLayout } from '../GameNightPlanningLayout';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

describe('GameNightPlanningLayout', () => {
  beforeEach(() => {
    useGameNightStore.setState(useGameNightStore.getInitialState());
  });

  it('renders two-column layout', () => {
    render(<GameNightPlanningLayout title="Friday Night" />);
    expect(screen.getByTestId('planning-layout')).toBeInTheDocument();
  });

  it('shows planning title', () => {
    render(<GameNightPlanningLayout title="Friday Night" />);
    expect(screen.getByText('Friday Night')).toBeInTheDocument();
  });

  it('shows dealt cards area', () => {
    render(<GameNightPlanningLayout title="Test" />);
    expect(screen.getByTestId('dealt-cards-area')).toBeInTheDocument();
  });

  it('shows empty drop zone when no games selected', () => {
    render(<GameNightPlanningLayout title="Test" />);
    expect(screen.getByText(/aggiungi giochi/i)).toBeInTheDocument();
  });

  it('shows player section', () => {
    render(<GameNightPlanningLayout title="Test" />);
    expect(screen.getAllByText(/giocatori/i)[0]).toBeInTheDocument();
  });

  it('shows timeline section', () => {
    render(<GameNightPlanningLayout title="Test" />);
    expect(screen.getAllByText(/programma/i)[0]).toBeInTheDocument();
  });

  it('renders AI suggestion card in left column', () => {
    render(<GameNightPlanningLayout title="Test" />);
    expect(screen.getByText(/suggerimenti ai/i)).toBeInTheDocument();
  });
});
