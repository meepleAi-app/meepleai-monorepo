import { render, screen } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import { HowItWorksSteps } from '../HowItWorksSteps';

expect.extend(toHaveNoViolations);

describe('HowItWorksSteps', () => {
  it('renders section with id="come-funziona"', () => {
    const { container } = render(<HowItWorksSteps />);
    expect(container.querySelector('#come-funziona')).toBeInTheDocument();
  });

  it('renders section heading', () => {
    render(<HowItWorksSteps />);
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('Come funziona');
  });

  it('renders 4 step numbers', () => {
    render(<HowItWorksSteps />);
    for (const num of ['1', '2', '3', '4']) {
      expect(screen.getByText(num)).toBeInTheDocument();
    }
  });

  it('renders 4 step headings as h3', () => {
    render(<HowItWorksSteps />);
    expect(screen.getAllByRole('heading', { level: 3 })).toHaveLength(4);
  });

  it('mostra 4 sezioni di valore', () => {
    render(<HowItWorksSteps />);
    expect(screen.getAllByRole('listitem').length).toBe(4);
  });

  it('mostra il pain point delle regole', () => {
    render(<HowItWorksSteps />);
    expect(screen.getByText(/regola subito/i)).toBeInTheDocument();
  });

  it('mostra il pain point delle dispute', () => {
    render(<HowItWorksSteps />);
    expect(screen.getByText(/niente più dispute/i)).toBeInTheDocument();
  });

  it('mostra il pain point della serata', () => {
    render(<HowItWorksSteps />);
    expect(screen.getByText(/serata salvata/i)).toBeInTheDocument();
  });

  it('mostra la storia di gioco', () => {
    render(<HowItWorksSteps />);
    expect(screen.getByText(/ricorda tutto/i)).toBeInTheDocument();
  });

  it('ha landmark section con id come-funziona', () => {
    render(<HowItWorksSteps />);
    expect(document.getElementById('come-funziona')).toBeInTheDocument();
  });

  it('has no accessibility violations', async () => {
    const { container } = render(<HowItWorksSteps />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
