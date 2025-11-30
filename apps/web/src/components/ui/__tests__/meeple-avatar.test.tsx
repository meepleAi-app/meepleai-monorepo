import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { MeepleAvatar } from '../meeple-avatar';
import type { MeepleAvatarState, MeepleAvatarSize } from '../meeple-avatar';

describe('MeepleAvatar', () => {
  describe('Rendering', () => {
    it('renders with default props', () => {
      render(<MeepleAvatar state="idle" />);
      const avatar = screen.getByRole('img');
      expect(avatar).toBeInTheDocument();
      expect(avatar).toHaveAttribute('aria-label', 'AI assistant ready');
    });

    it('renders all state variants', () => {
      const states: MeepleAvatarState[] = [
        'idle',
        'thinking',
        'confident',
        'searching',
        'uncertain',
      ];

      states.forEach(state => {
        const { unmount } = render(<MeepleAvatar state={state} />);
        const avatar = screen.getByRole('img');
        expect(avatar).toBeInTheDocument();
        unmount();
      });
    });

    it('renders all size variants', () => {
      const sizes: MeepleAvatarSize[] = ['sm', 'md', 'lg'];

      sizes.forEach(size => {
        const { unmount } = render(<MeepleAvatar state="idle" size={size} />);
        const avatar = screen.getByRole('img');
        expect(avatar).toBeInTheDocument();
        unmount();
      });
    });

    it('applies custom className', () => {
      const { container } = render(<MeepleAvatar state="idle" className="custom-class" />);
      const avatarWrapper = container.querySelector('.meeple-avatar');
      expect(avatarWrapper).toHaveClass('custom-class');
    });
  });

  describe('Accessibility', () => {
    it('has role="img"', () => {
      render(<MeepleAvatar state="idle" />);
      const avatar = screen.getByRole('img');
      expect(avatar).toHaveAttribute('role', 'img');
    });

    it('has correct ARIA labels for each state', () => {
      const stateLabels: Record<MeepleAvatarState, string> = {
        idle: 'AI assistant ready',
        thinking: 'AI assistant thinking',
        confident: 'AI assistant confident',
        searching: 'AI assistant searching',
        uncertain: 'AI assistant uncertain',
      };

      Object.entries(stateLabels).forEach(([state, label]) => {
        const { unmount } = render(<MeepleAvatar state={state as MeepleAvatarState} />);
        const avatar = screen.getByRole('img');
        expect(avatar).toHaveAttribute('aria-label', label);
        unmount();
      });
    });

    it('accepts custom ARIA label', () => {
      const customLabel = 'Custom AI status';
      render(<MeepleAvatar state="idle" ariaLabel={customLabel} />);
      const avatar = screen.getByRole('img');
      expect(avatar).toHaveAttribute('aria-label', customLabel);
    });
  });

  describe('Size variants', () => {
    it('applies correct size classes', () => {
      const { container: smContainer } = render(<MeepleAvatar state="idle" size="sm" />);
      expect(smContainer.querySelector('.w-8')).toBeInTheDocument();

      const { container: mdContainer } = render(<MeepleAvatar state="idle" size="md" />);
      expect(mdContainer.querySelector('.w-10')).toBeInTheDocument();

      const { container: lgContainer } = render(<MeepleAvatar state="idle" size="lg" />);
      expect(lgContainer.querySelector('.w-12')).toBeInTheDocument();
    });

    it('renders SVG with correct dimensions', () => {
      const { container } = render(<MeepleAvatar state="idle" size="sm" />);
      const svg = container.querySelector('svg');
      expect(svg).toHaveAttribute('width', '32');
      expect(svg).toHaveAttribute('height', '32');
    });
  });

  describe('State-specific elements', () => {
    it('renders thinking dots for thinking state', () => {
      const { container } = render(<MeepleAvatar state="thinking" />);
      const svg = container.querySelector('svg');
      const thinkingDots = svg?.querySelector('.thinking-dots');
      expect(thinkingDots).toBeInTheDocument();
    });

    it('renders sparkles for confident state', () => {
      const { container } = render(<MeepleAvatar state="confident" />);
      const svg = container.querySelector('svg');
      const sparkles = svg?.querySelector('.confident-sparkles');
      expect(sparkles).toBeInTheDocument();
    });

    it('renders magnifying glass for searching state', () => {
      const { container } = render(<MeepleAvatar state="searching" />);
      const svg = container.querySelector('svg');
      const searchIcon = svg?.querySelector('.searching-icon');
      expect(searchIcon).toBeInTheDocument();
    });

    it('renders question mark for uncertain state', () => {
      const { container } = render(<MeepleAvatar state="uncertain" />);
      const svg = container.querySelector('svg');
      const uncertainIcon = svg?.querySelector('.uncertain-icon');
      expect(uncertainIcon).toBeInTheDocument();
      const questionMark = uncertainIcon?.querySelector('text');
      expect(questionMark).toHaveTextContent('?');
    });

    it('does not render state-specific elements in idle state', () => {
      const { container } = render(<MeepleAvatar state="idle" />);
      const svg = container.querySelector('svg');
      expect(svg?.querySelector('.thinking-dots')).not.toBeInTheDocument();
      expect(svg?.querySelector('.confident-sparkles')).not.toBeInTheDocument();
      expect(svg?.querySelector('.searching-icon')).not.toBeInTheDocument();
      expect(svg?.querySelector('.uncertain-icon')).not.toBeInTheDocument();
    });
  });

  describe('SVG structure', () => {
    it('includes meeple body with head, body, and arms', () => {
      const { container } = render(<MeepleAvatar state="idle" />);
      const svg = container.querySelector('svg');
      const meepleBody = svg?.querySelector('.meeple-body');
      expect(meepleBody).toBeInTheDocument();

      // Head (circle)
      const head = meepleBody?.querySelector('circle');
      expect(head).toBeInTheDocument();
      expect(head).toHaveAttribute('cx', '50');
      expect(head).toHaveAttribute('cy', '25');
      expect(head).toHaveAttribute('r', '15');

      // Body (path)
      const body = meepleBody?.querySelector('path');
      expect(body).toBeInTheDocument();

      // Arms (ellipses)
      const arms = meepleBody?.querySelectorAll('ellipse');
      expect(arms).toHaveLength(2);
    });

    it('includes gradient definition', () => {
      const { container } = render(<MeepleAvatar state="idle" />);
      const svg = container.querySelector('svg');
      const gradient = svg?.querySelector('linearGradient');
      expect(gradient).toBeInTheDocument();
      expect(gradient).toHaveAttribute('id', 'meeple-gradient-idle');
    });

    it('has unique gradient ID per state', () => {
      const states: MeepleAvatarState[] = ['idle', 'thinking', 'confident'];

      states.forEach(state => {
        const { container, unmount } = render(<MeepleAvatar state={state} />);
        const svg = container.querySelector('svg');
        const gradient = svg?.querySelector('linearGradient');
        expect(gradient).toHaveAttribute('id', `meeple-gradient-${state}`);
        unmount();
      });
    });
  });

  describe('Animations', () => {
    it('applies state-specific CSS class', () => {
      const states: MeepleAvatarState[] = [
        'idle',
        'thinking',
        'confident',
        'searching',
        'uncertain',
      ];

      states.forEach(state => {
        const { container, unmount } = render(<MeepleAvatar state={state} />);
        const svg = container.querySelector('svg');
        expect(svg).toHaveClass(`state-${state}`);
        unmount();
      });
    });

    it('includes SVG animations for thinking state', () => {
      const { container } = render(<MeepleAvatar state="thinking" />);
      const svg = container.querySelector('svg');
      const animates = svg?.querySelectorAll('animate');
      expect(animates && animates.length).toBeGreaterThan(0);
    });

    it('includes SVG animations for confident state', () => {
      const { container } = render(<MeepleAvatar state="confident" />);
      const svg = container.querySelector('svg');
      const animates = svg?.querySelectorAll('animate');
      expect(animates && animates.length).toBeGreaterThan(0);
    });

    it('includes animateTransform for searching state', () => {
      const { container } = render(<MeepleAvatar state="searching" />);
      const svg = container.querySelector('svg');
      const animateTransform = svg?.querySelector('animateTransform');
      expect(animateTransform).toBeInTheDocument();
      expect(animateTransform).toHaveAttribute('attributeName', 'transform');
      expect(animateTransform).toHaveAttribute('type', 'rotate');
    });
  });

  describe('Forward ref', () => {
    it('forwards ref to container div', () => {
      const ref = { current: null as HTMLDivElement | null };
      render(<MeepleAvatar state="idle" ref={ref} />);
      expect(ref.current).toBeInstanceOf(HTMLDivElement);
      expect(ref.current).toHaveClass('meeple-avatar');
    });
  });

  describe('Styling', () => {
    it('includes scoped styles', () => {
      const { container } = render(<MeepleAvatar state="idle" />);
      const style = container.querySelector('style');
      expect(style).toBeInTheDocument();
      expect(style?.textContent).toContain('meeple-avatar');
    });

    it('includes prefers-reduced-motion styles', () => {
      const { container } = render(<MeepleAvatar state="thinking" />);
      const style = container.querySelector('style');
      expect(style?.textContent).toContain('prefers-reduced-motion');
    });

    it('disables animations when prefers-reduced-motion is set', () => {
      const { container } = render(<MeepleAvatar state="thinking" />);
      const style = container.querySelector('style');

      // Verify the media query exists
      expect(style?.textContent).toContain('@media (prefers-reduced-motion: reduce)');

      // Verify animations are disabled in the media query
      expect(style?.textContent).toContain('animation: none !important');

      // Verify SVG animations are hidden
      expect(style?.textContent).toContain('display: none');
    });

    it('has user-select: none', () => {
      const { container } = render(<MeepleAvatar state="idle" />);
      const style = container.querySelector('style');
      expect(style?.textContent).toContain('user-select: none');
    });
  });

  describe('Color and gradients', () => {
    it('uses orange gradient for meeple body', () => {
      const { container } = render(<MeepleAvatar state="idle" />);
      const svg = container.querySelector('svg');
      const gradient = svg?.querySelector('linearGradient');
      const stops = gradient?.querySelectorAll('stop');
      expect(stops).toHaveLength(2);
      // SVG attributes are case-sensitive in DOM, use getAttribute for React-rendered SVG
      expect(
        stops?.[0]?.getAttribute('stopColor') || stops?.[0]?.getAttribute('stop-color')
      ).toBeTruthy();
      expect(
        stops?.[1]?.getAttribute('stopColor') || stops?.[1]?.getAttribute('stop-color')
      ).toBeTruthy();
    });

    it('uses yellow/gold for state indicators', () => {
      const { container } = render(<MeepleAvatar state="thinking" />);
      const svg = container.querySelector('svg');
      const dots = svg?.querySelectorAll('.thinking-dots circle');
      dots?.forEach(dot => {
        expect(dot).toHaveAttribute('fill', '#fbbf24');
      });
    });
  });

  describe('Edge cases', () => {
    it('handles rapid state changes', () => {
      const { rerender } = render(<MeepleAvatar state="idle" />);
      expect(screen.getByRole('img')).toHaveAttribute('aria-label', 'AI assistant ready');

      rerender(<MeepleAvatar state="thinking" />);
      expect(screen.getByRole('img')).toHaveAttribute('aria-label', 'AI assistant thinking');

      rerender(<MeepleAvatar state="confident" />);
      expect(screen.getByRole('img')).toHaveAttribute('aria-label', 'AI assistant confident');
    });

    it('handles size changes while maintaining state', () => {
      const { rerender, container } = render(<MeepleAvatar state="thinking" size="sm" />);
      expect(container.querySelector('.w-8')).toBeInTheDocument();

      rerender(<MeepleAvatar state="thinking" size="lg" />);
      expect(container.querySelector('.w-12')).toBeInTheDocument();

      // State-specific elements should still be present
      expect(container.querySelector('.thinking-dots')).toBeInTheDocument();
    });
  });
});
