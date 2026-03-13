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

  it('renders all 4 step titles', () => {
    render(<HowItWorksSteps />);
    expect(screen.getByText('Trova il gioco')).toBeInTheDocument();
    expect(screen.getByText('Carica le regole')).toBeInTheDocument();
    expect(screen.getByText("Gioca con l'arbitro AI")).toBeInTheDocument();
    expect(screen.getByText('Salva e riprendi')).toBeInTheDocument();
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

  it('has no accessibility violations', async () => {
    const { container } = render(<HowItWorksSteps />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
