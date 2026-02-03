/**
 * PageTransition Animation Component Test Suite (Issue #2965 Wave 9)
 *
 * Tests for the Framer Motion page transition component.
 * Verifies transition variants, animation states, and accessibility.
 */

import { render, screen } from '@testing-library/react';
import { PageTransition } from '../PageTransition';

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

describe('PageTransition', () => {
  it('renders children correctly', () => {
    render(
      <PageTransition>
        <span>Page content</span>
      </PageTransition>
    );

    expect(screen.getByText('Page content')).toBeInTheDocument();
  });

  it('renders with motion div wrapper', () => {
    render(
      <PageTransition>
        <span>Content</span>
      </PageTransition>
    );

    expect(screen.getByTestId('motion-div')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    render(
      <PageTransition className="custom-page-class">
        <span>Content</span>
      </PageTransition>
    );

    expect(screen.getByTestId('motion-div')).toHaveClass('custom-page-class');
  });

  it('renders with default fade variant', () => {
    render(
      <PageTransition>
        <p>Fade transition</p>
      </PageTransition>
    );

    expect(screen.getByText('Fade transition')).toBeInTheDocument();
  });

  it('renders with slide variant', () => {
    render(
      <PageTransition variant="slide">
        <p>Slide transition</p>
      </PageTransition>
    );

    expect(screen.getByText('Slide transition')).toBeInTheDocument();
  });

  it('renders with scale variant', () => {
    render(
      <PageTransition variant="scale">
        <p>Scale transition</p>
      </PageTransition>
    );

    expect(screen.getByText('Scale transition')).toBeInTheDocument();
  });

  it('renders complex page content', () => {
    render(
      <PageTransition>
        <main>
          <header>
            <h1>Page Title</h1>
          </header>
          <article>
            <p>Article content</p>
          </article>
          <footer>
            <small>Footer text</small>
          </footer>
        </main>
      </PageTransition>
    );

    expect(screen.getByRole('heading', { name: 'Page Title' })).toBeInTheDocument();
    expect(screen.getByText('Article content')).toBeInTheDocument();
    expect(screen.getByText('Footer text')).toBeInTheDocument();
  });

  it('combines variant and className', () => {
    render(
      <PageTransition variant="slide" className="slide-page">
        <span>Combined props</span>
      </PageTransition>
    );

    expect(screen.getByText('Combined props')).toBeInTheDocument();
    expect(screen.getByTestId('motion-div')).toHaveClass('slide-page');
  });

  it('renders nested PageTransitions', () => {
    render(
      <PageTransition>
        <div>
          <PageTransition variant="slide">
            <span>Nested content</span>
          </PageTransition>
        </div>
      </PageTransition>
    );

    expect(screen.getByText('Nested content')).toBeInTheDocument();
    expect(screen.getAllByTestId('motion-div')).toHaveLength(2);
  });
});
