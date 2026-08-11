/** @vitest-environment jsdom */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { GUARDRAIL_DESCRIPTIONS, ValidationBadges, ValidationLegend } from '../ClaimsSection';

describe('ValidationLegend (#539)', () => {
  it('renders the guardrail taxonomy with a label per T-rule', () => {
    render(<ValidationLegend />);
    expect(screen.getByText(/Cosa sono i badge/i)).toBeInTheDocument();
    for (const { label } of Object.values(GUARDRAIL_DESCRIPTIONS)) {
      expect(screen.getByText(label)).toBeInTheDocument();
    }
  });

  it('covers the five ADR-051 guardrail families', () => {
    expect(Object.keys(GUARDRAIL_DESCRIPTIONS)).toEqual(
      expect.arrayContaining(['T1', 'T2', 'T3a', 'T3b', 'T4'])
    );
  });
});

describe('ValidationBadges tooltip (#539)', () => {
  it('embeds the guardrail description + outcome in the badge title', () => {
    render(<ValidationBadges validations={[{ rule: 'T4', outcome: 'fail', message: null }]} />);
    const badge = screen.getByTestId('claim-validation-badge-T4');
    expect(badge).toHaveAttribute('title', expect.stringContaining('Pagina/substring'));
    expect(badge).toHaveAttribute('title', expect.stringContaining('Esito: fail'));
  });
});
