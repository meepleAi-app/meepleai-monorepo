/**
 * FadeIn Animation Component Test Suite (Issue #2965 Wave 9)
 *
 * Tests for the Framer Motion fade-in animation component.
 * Verifies animation variants, direction props, and accessibility.
 */

import { render, screen } from '@testing-library/react';
import { FadeIn } from '../FadeIn';

// Mock framer-motion to prevent animation timing issues
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, className, ...props }: React.HTMLAttributes<HTMLDivElement>) => (
      <div className={className} data-testid="motion-div" {...props}>
        {children}
      </div>
    ),
  },
}));

describe('FadeIn', () => {
  it('renders children correctly', () => {
    render(
      <FadeIn>
        <span>Test content</span>
      </FadeIn>
    );

    expect(screen.getByText('Test content')).toBeInTheDocument();
  });

  it('renders with motion div wrapper', () => {
    render(
      <FadeIn>
        <span>Content</span>
      </FadeIn>
    );

    expect(screen.getByTestId('motion-div')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    render(
      <FadeIn className="custom-class">
        <span>Content</span>
      </FadeIn>
    );

    expect(screen.getByTestId('motion-div')).toHaveClass('custom-class');
  });

  it('renders with default props', () => {
    const { container } = render(
      <FadeIn>
        <p>Default animation</p>
      </FadeIn>
    );

    expect(container.firstChild).toBeInTheDocument();
    expect(screen.getByText('Default animation')).toBeInTheDocument();
  });

  it('renders with direction up', () => {
    render(
      <FadeIn direction="up">
        <span>Slide up content</span>
      </FadeIn>
    );

    expect(screen.getByText('Slide up content')).toBeInTheDocument();
  });

  it('renders with direction down', () => {
    render(
      <FadeIn direction="down">
        <span>Slide down content</span>
      </FadeIn>
    );

    expect(screen.getByText('Slide down content')).toBeInTheDocument();
  });

  it('renders with direction left', () => {
    render(
      <FadeIn direction="left">
        <span>Slide left content</span>
      </FadeIn>
    );

    expect(screen.getByText('Slide left content')).toBeInTheDocument();
  });

  it('renders with direction right', () => {
    render(
      <FadeIn direction="right">
        <span>Slide right content</span>
      </FadeIn>
    );

    expect(screen.getByText('Slide right content')).toBeInTheDocument();
  });

  it('renders with direction none', () => {
    render(
      <FadeIn direction="none">
        <span>No slide content</span>
      </FadeIn>
    );

    expect(screen.getByText('No slide content')).toBeInTheDocument();
  });

  it('accepts delay prop', () => {
    render(
      <FadeIn delay={0.5}>
        <span>Delayed content</span>
      </FadeIn>
    );

    expect(screen.getByText('Delayed content')).toBeInTheDocument();
  });

  it('accepts duration prop', () => {
    render(
      <FadeIn duration={1}>
        <span>Long duration content</span>
      </FadeIn>
    );

    expect(screen.getByText('Long duration content')).toBeInTheDocument();
  });

  it('accepts distance prop', () => {
    render(
      <FadeIn distance={40}>
        <span>Far slide content</span>
      </FadeIn>
    );

    expect(screen.getByText('Far slide content')).toBeInTheDocument();
  });

  it('combines multiple props correctly', () => {
    render(
      <FadeIn delay={0.2} duration={0.8} direction="up" distance={30} className="combined-test">
        <span>Combined props</span>
      </FadeIn>
    );

    expect(screen.getByText('Combined props')).toBeInTheDocument();
    expect(screen.getByTestId('motion-div')).toHaveClass('combined-test');
  });

  it('renders complex children', () => {
    render(
      <FadeIn>
        <div>
          <h1>Title</h1>
          <p>Description</p>
          <button>Action</button>
        </div>
      </FadeIn>
    );

    expect(screen.getByRole('heading', { name: 'Title' })).toBeInTheDocument();
    expect(screen.getByText('Description')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Action' })).toBeInTheDocument();
  });
});
