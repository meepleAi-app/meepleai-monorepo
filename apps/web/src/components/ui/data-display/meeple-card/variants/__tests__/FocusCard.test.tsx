import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

import { FocusCard } from '../FocusCard';

describe('FocusCard connections path', () => {
  it('S2: connections=[] renders no chip strip, no warn', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    render(<FocusCard entity="game" title="X" connections={[]} />);
    expect(screen.queryByTestId('connection-chip-strip')).toBeNull();
    expect(warn).not.toHaveBeenCalled();
  });

  it('renders ConnectionChipStrip when connections has items', () => {
    render(
      <FocusCard entity="game" title="X" connections={[{ entityType: 'session', count: 3 }]} />
    );
    expect(screen.getByTestId('connection-chip-strip')).toBeInTheDocument();
  });
});
