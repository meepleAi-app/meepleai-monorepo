import { render, screen } from '@testing-library/react';
import { describe, expect, it, beforeEach, vi } from 'vitest';

import { useSessionStore } from '@/store/session';
import { LiveSessionLayout } from '../LiveSessionLayout';

vi.mock('next/navigation', () => ({ usePathname: () => '/sessions/s1' }));

describe('LiveSessionLayout', () => {
  beforeEach(() => {
    useSessionStore.setState(useSessionStore.getInitialState());
  });

  it('renders three columns on desktop', () => {
    render(
      <LiveSessionLayout
        leftPanel={<div>Left</div>}
        centerContent={<div>Center</div>}
        rightPanel={<div>Right</div>}
      />
    );
    expect(screen.getByText('Left')).toBeInTheDocument();
    expect(screen.getByText('Center')).toBeInTheDocument();
    expect(screen.getByText('Right')).toBeInTheDocument();
  });

  it('renders with data-testid', () => {
    render(
      <LiveSessionLayout
        leftPanel={<div>L</div>}
        centerContent={<div>C</div>}
        rightPanel={<div>R</div>}
      />
    );
    expect(screen.getByTestId('live-session-layout')).toBeInTheDocument();
  });
});
