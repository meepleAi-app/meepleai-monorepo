import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ChatHistoryBanner } from '../ChatHistoryBanner';

describe('ChatHistoryBanner', () => {
  it('renders disclaimer text about historical messages', () => {
    render(<ChatHistoryBanner />);
    expect(screen.getByText(/messaggi precedenti.*sola lettura/i)).toBeInTheDocument();
    expect(screen.getByText(/citazioni.*non sono ricostruibili/i)).toBeInTheDocument();
  });

  it('passes className prop to root element', () => {
    const { container } = render(<ChatHistoryBanner className="custom-class" />);
    expect(container.firstChild).toHaveClass('custom-class');
  });
});
