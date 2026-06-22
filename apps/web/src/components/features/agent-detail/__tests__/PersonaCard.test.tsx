/**
 * PersonaCard unit tests — Wave C.2 Task 2
 *
 * 3 tests: render with persona, empty state, data-slot attribute.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { PersonaCard } from '../PersonaCard';

const LABELS = {
  title: 'Persona',
  empty: 'Nessuna persona configurata.',
};

describe('PersonaCard', () => {
  it('renders data-slot attribute', () => {
    render(<PersonaCard persona="Esperto di strategia" labels={LABELS} />);
    expect(document.querySelector('[data-slot="agent-detail-persona-card"]')).toBeTruthy();
  });

  it('renders persona text when provided', () => {
    render(<PersonaCard persona="Esperto di strategia" labels={LABELS} />);
    expect(screen.getByText(/esperto di strategia/i)).toBeInTheDocument();
  });

  it('renders empty state when persona is null', () => {
    render(<PersonaCard persona={null} labels={LABELS} />);
    expect(screen.getByText(/nessuna persona configurata/i)).toBeInTheDocument();
  });
});
