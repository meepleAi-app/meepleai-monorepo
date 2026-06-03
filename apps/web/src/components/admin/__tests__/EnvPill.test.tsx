/**
 * EnvPill Component Tests (Issue #1836)
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { EnvPill } from '../EnvPill';

describe('EnvPill', () => {
  it('renders dev variant with the dev label', () => {
    render(<EnvPill env="dev" />);

    const pill = screen.getByTestId('env-pill-dev');
    expect(pill).toBeInTheDocument();
    expect(pill).toHaveTextContent('dev');
    expect(pill).toHaveAttribute('aria-label', expect.stringContaining('dev'));
  });

  it('renders stg variant', () => {
    render(<EnvPill env="stg" />);

    const pill = screen.getByTestId('env-pill-stg');
    expect(pill).toHaveTextContent('stg');
  });

  it('renders prd variant', () => {
    render(<EnvPill env="prd" />);

    const pill = screen.getByTestId('env-pill-prd');
    expect(pill).toHaveTextContent('prd');
  });

  it('omits the env prefix in compact mode', () => {
    render(<EnvPill env="prd" compact />);

    const pill = screen.getByTestId('env-pill-prd');
    // Only the label is rendered, not the "env" prefix.
    expect(pill.textContent).toBe('prd');
  });

  it('exposes an aria-label for screen readers', () => {
    render(<EnvPill env="stg" />);

    expect(screen.getByLabelText(/Environment scope: stg/i)).toBeInTheDocument();
  });
});
