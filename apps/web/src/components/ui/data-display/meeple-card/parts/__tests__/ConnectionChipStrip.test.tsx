import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';

import { ConnectionChipStrip } from '../ConnectionChipStrip';

describe('ConnectionChipStrip', () => {
  it('returns null when connections array is empty', () => {
    const { container } = render(<ConnectionChipStrip variant="footer" connections={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders a chip per connection (footer variant)', () => {
    render(
      <ConnectionChipStrip
        variant="footer"
        connections={[
          { entityType: 'kb', count: 3 },
          { entityType: 'session', count: 5 },
        ]}
      />
    );
    expect(screen.getAllByRole('button')).toHaveLength(2);
  });

  it('renders smaller chips in inline variant by default', () => {
    const { container } = render(
      <ConnectionChipStrip variant="inline" connections={[{ entityType: 'kb', count: 3 }]} />
    );
    const chipFace = container.querySelector('span[style*="width: 22px"]');
    expect(chipFace).toBeTruthy();
  });

  it('renders larger chips in footer variant by default', () => {
    const { container } = render(
      <ConnectionChipStrip variant="footer" connections={[{ entityType: 'kb', count: 3 }]} />
    );
    const chipFace = container.querySelector('span[style*="width: 28px"]');
    expect(chipFace).toBeTruthy();
  });
});
