import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { CompactCard } from '../CompactCard';

describe('CompactCard connections path', () => {
  it('S10: connections=[...] renders no strip (documented no-op)', () => {
    render(
      <CompactCard entity="game" title="X" connections={[{ entityType: 'session', count: 3 }]} />
    );
    expect(screen.queryByTestId('connection-chip-strip')).toBeNull();
  });
});
