import { render } from '@testing-library/react';
import { StatusGlow } from '../StatusGlow';

describe('StatusGlow', () => {
  it('renders pulsing animation for active state', () => {
    const { container } = render(<StatusGlow state="active" entityColor="25 95% 45%" />);
    const el = container.firstChild as HTMLElement;
    expect(el.className).toContain('animate-pulse');
  });

  it('renders solid glow without pulse for complete state', () => {
    const { container } = render(<StatusGlow state="complete" entityColor="25 95% 45%" />);
    const el = container.firstChild as HTMLElement;
    expect(el.className).not.toContain('animate-pulse');
  });

  it('renders with zero opacity for idle state', () => {
    const { container } = render(<StatusGlow state="idle" entityColor="25 95% 45%" />);
    const el = container.firstChild as HTMLElement;
    expect(el.className).toContain('opacity-0');
  });

  it('uses red color override for error state', () => {
    const { container } = render(<StatusGlow state="error" entityColor="25 95% 45%" />);
    const el = container.firstChild as HTMLElement;
    expect(el.className).toContain('animate-pulse');
    // Should use red, not entity color
    expect(el.style.getPropertyValue('--glow-color')).toContain('0');
  });

  it('renders ring for new state', () => {
    const { container } = render(<StatusGlow state="new" entityColor="25 95% 45%" />);
    const el = container.firstChild as HTMLElement;
    expect(el.className).toContain('ring-2');
  });
});
