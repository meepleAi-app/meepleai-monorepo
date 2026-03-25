import { render, screen } from '@testing-library/react';
import { DashboardScrollRow } from '../DashboardScrollRow';

describe('DashboardScrollRow', () => {
  it('renders children', () => {
    render(
      <DashboardScrollRow>
        <div data-testid="child">A</div>
      </DashboardScrollRow>
    );
    expect(screen.getByTestId('child')).toBeInTheDocument();
  });

  it('has scroll-snap classes', () => {
    const { container } = render(<DashboardScrollRow>content</DashboardScrollRow>);
    const el = container.firstChild as HTMLElement;
    expect(el.className).toContain('snap-x');
    expect(el.className).toContain('overflow-x-auto');
  });
});
