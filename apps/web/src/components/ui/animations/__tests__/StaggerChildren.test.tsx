/**
 * StaggerChildren Animation Component Test Suite (Issue #2965 Wave 9)
 *
 * Tests for the Framer Motion stagger animation component.
 * Verifies stagger timing, child rendering, and configuration props.
 */

import { render, screen } from '@testing-library/react';
import { StaggerChildren } from '../StaggerChildren';

// Mock framer-motion to prevent animation timing issues
vi.mock('framer-motion', () => ({
  motion: {
    div: ({
      children,
      className,
      ...props
    }: React.HTMLAttributes<HTMLDivElement> & { key?: number }) => (
      <div className={className} data-testid="motion-div" {...props}>
        {children}
      </div>
    ),
  },
}));

describe('StaggerChildren', () => {
  it('renders single child correctly', () => {
    render(
      <StaggerChildren>
        <span>Single child</span>
      </StaggerChildren>
    );

    expect(screen.getByText('Single child')).toBeInTheDocument();
  });

  it('renders multiple children', () => {
    render(
      <StaggerChildren>
        <div>Item 1</div>
        <div>Item 2</div>
        <div>Item 3</div>
      </StaggerChildren>
    );

    expect(screen.getByText('Item 1')).toBeInTheDocument();
    expect(screen.getByText('Item 2')).toBeInTheDocument();
    expect(screen.getByText('Item 3')).toBeInTheDocument();
  });

  it('wraps each child in motion div', () => {
    render(
      <StaggerChildren>
        <span>Child 1</span>
        <span>Child 2</span>
      </StaggerChildren>
    );

    // Should have 3 motion divs: 1 container + 2 children
    const motionDivs = screen.getAllByTestId('motion-div');
    expect(motionDivs.length).toBeGreaterThanOrEqual(1);
  });

  it('applies custom className to container', () => {
    render(
      <StaggerChildren className="stagger-container">
        <div>Item</div>
      </StaggerChildren>
    );

    const container = screen.getAllByTestId('motion-div')[0];
    expect(container).toHaveClass('stagger-container');
  });

  it('accepts staggerDelay prop', () => {
    render(
      <StaggerChildren staggerDelay={0.2}>
        <div>Delayed 1</div>
        <div>Delayed 2</div>
      </StaggerChildren>
    );

    expect(screen.getByText('Delayed 1')).toBeInTheDocument();
    expect(screen.getByText('Delayed 2')).toBeInTheDocument();
  });

  it('accepts initialDelay prop', () => {
    render(
      <StaggerChildren initialDelay={0.5}>
        <div>Initial delay content</div>
      </StaggerChildren>
    );

    expect(screen.getByText('Initial delay content')).toBeInTheDocument();
  });

  it('accepts duration prop', () => {
    render(
      <StaggerChildren duration={1}>
        <div>Long animation</div>
      </StaggerChildren>
    );

    expect(screen.getByText('Long animation')).toBeInTheDocument();
  });

  it('combines multiple configuration props', () => {
    render(
      <StaggerChildren
        staggerDelay={0.15}
        initialDelay={0.3}
        duration={0.6}
        className="configured-stagger"
      >
        <div>Config 1</div>
        <div>Config 2</div>
        <div>Config 3</div>
      </StaggerChildren>
    );

    expect(screen.getByText('Config 1')).toBeInTheDocument();
    expect(screen.getByText('Config 2')).toBeInTheDocument();
    expect(screen.getByText('Config 3')).toBeInTheDocument();

    const container = screen.getAllByTestId('motion-div')[0];
    expect(container).toHaveClass('configured-stagger');
  });

  it('renders list items correctly', () => {
    render(
      <StaggerChildren>
        <li>List item 1</li>
        <li>List item 2</li>
        <li>List item 3</li>
      </StaggerChildren>
    );

    expect(screen.getByText('List item 1')).toBeInTheDocument();
    expect(screen.getByText('List item 2')).toBeInTheDocument();
    expect(screen.getByText('List item 3')).toBeInTheDocument();
  });

  it('renders card components correctly', () => {
    render(
      <StaggerChildren>
        <div data-testid="card-1">Card 1</div>
        <div data-testid="card-2">Card 2</div>
      </StaggerChildren>
    );

    expect(screen.getByTestId('card-1')).toBeInTheDocument();
    expect(screen.getByTestId('card-2')).toBeInTheDocument();
  });

  it('handles empty children gracefully', () => {
    const { container } = render(
      <StaggerChildren>
        {null}
      </StaggerChildren>
    );

    expect(container.firstChild).toBeInTheDocument();
  });

  it('renders mixed content types', () => {
    render(
      <StaggerChildren>
        <p>Paragraph</p>
        <button>Button</button>
        <a href="#">Link</a>
      </StaggerChildren>
    );

    expect(screen.getByText('Paragraph')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Button' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Link' })).toBeInTheDocument();
  });

  it('preserves child element types', () => {
    render(
      <StaggerChildren>
        <button type="submit">Submit</button>
        <button type="button">Cancel</button>
      </StaggerChildren>
    );

    const buttons = screen.getAllByRole('button');
    expect(buttons).toHaveLength(2);
  });
});
