import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { QualityBandChip } from '../QualityBandChip';

describe('QualityBandChip', () => {
  it('renders the em-dash placeholder when band is null', () => {
    render(<QualityBandChip band={null} />);
    expect(screen.getByTestId('quality-band-chip-empty')).toBeInTheDocument();
    expect(screen.getByTestId('quality-band-chip-empty')).toHaveTextContent('—');
  });

  it.each(['Green', 'Yellow', 'Red'] as const)('renders the %s band variant', band => {
    render(<QualityBandChip band={band} />);
    expect(screen.getByTestId(`quality-band-chip-${band.toLowerCase()}`)).toBeInTheDocument();
  });

  it('localises the label to IT for Green', () => {
    render(<QualityBandChip band="Green" />);
    expect(screen.getByTestId('quality-band-chip-green')).toHaveTextContent('Verde');
  });
});
