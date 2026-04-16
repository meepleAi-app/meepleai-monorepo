import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { StatusBadge } from '@/components/ui/data-display/meeple-card/parts/StatusBadge';

describe('StatusBadge', () => {
  it('renders "Posseduto" label for owned status (Italian localization)', () => {
    render(<StatusBadge status="owned" />);
    expect(screen.getByText('Posseduto')).toBeInTheDocument();
    expect(screen.queryByText('owned')).toBeNull();
  });

  it('falls back to raw status string when no localized label exists', () => {
    render(<StatusBadge status="wishlist" />);
    expect(screen.getByText('wishlist')).toBeInTheDocument();
  });

  it('renders null for unknown status (no color mapping)', () => {
    const { container } = render(
      // @ts-expect-error - intentionally invalid status to exercise null branch
      <StatusBadge status="definitely-not-a-status" />
    );
    expect(container.firstChild).toBeNull();
  });
});
