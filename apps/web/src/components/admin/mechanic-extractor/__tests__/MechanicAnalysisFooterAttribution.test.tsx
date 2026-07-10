/** @vitest-environment jsdom */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { MechanicAnalysisFooterAttribution } from '../MechanicAnalysisFooterAttribution';

describe('MechanicAnalysisFooterAttribution', () => {
  it('renders the ADR-051 attribution and NOT the forbidden Variant-C string', () => {
    render(<MechanicAnalysisFooterAttribution totalTokensUsed={500} estimatedCostUsd={0.002} />);
    expect(
      screen.getByText(/riformulata in parole originali e cita la pagina/i)
    ).toBeInTheDocument();
    expect(
      screen.queryByText(/non ha mai letto il testo del PDF originale/i)
    ).not.toBeInTheDocument();
    expect(screen.getByText(/500 tokens/)).toBeInTheDocument();
  });
});
