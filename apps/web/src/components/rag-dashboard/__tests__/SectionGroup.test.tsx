import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { SectionGroup } from '../SectionGroup';

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    section: ({
      children,
      ...props
    }: React.PropsWithChildren<Record<string, unknown>>) => (
      <section {...props}>{children}</section>
    ),
  },
}));

describe('SectionGroup', () => {
  it('should render icon, label, and description', () => {
    render(
      <SectionGroup
        id="test-group"
        label="Test Group"
        icon="🎯"
        description="Test description"
      >
        <div>Child content</div>
      </SectionGroup>
    );

    expect(screen.getByText('🎯')).toBeInTheDocument();
    expect(screen.getByText('Test Group')).toBeInTheDocument();
    expect(screen.getByText('Test description')).toBeInTheDocument();
  });

  it('should set id attribute for scroll targeting', () => {
    render(
      <SectionGroup id="test-section" label="Test" icon="🎯">
        <div>Content</div>
      </SectionGroup>
    );

    const section = document.getElementById('test-section');
    expect(section).toBeInTheDocument();
  });

  it('should apply scroll-mt-24 class', () => {
    render(
      <SectionGroup id="test-section" label="Test" icon="🎯">
        <div>Content</div>
      </SectionGroup>
    );

    const section = document.getElementById('test-section');
    expect(section).toHaveClass('scroll-mt-24');
  });

  it('should render children', () => {
    render(
      <SectionGroup id="test-section" label="Test" icon="🎯">
        <div data-testid="child-content">Child content here</div>
      </SectionGroup>
    );

    expect(screen.getByTestId('child-content')).toBeInTheDocument();
    expect(screen.getByText('Child content here')).toBeInTheDocument();
  });

  it('should apply custom className', () => {
    render(
      <SectionGroup
        id="test-section"
        label="Test"
        icon="🎯"
        className="custom-class"
      >
        <div>Content</div>
      </SectionGroup>
    );

    const section = document.getElementById('test-section');
    expect(section).toHaveClass('custom-class');
  });

  it('should render without description', () => {
    render(
      <SectionGroup id="test-section" label="Test" icon="🎯">
        <div>Content</div>
      </SectionGroup>
    );

    expect(screen.getByText('Test')).toBeInTheDocument();
    // No description paragraph should be rendered
    const heading = screen.getByRole('heading', { name: 'Test' });
    expect(heading.nextElementSibling).toBeNull();
  });

  it('should have correct heading structure', () => {
    render(
      <SectionGroup id="test-section" label="Group Label" icon="🎯">
        <div>Content</div>
      </SectionGroup>
    );

    const heading = screen.getByRole('heading', { level: 2, name: 'Group Label' });
    expect(heading).toBeInTheDocument();
    expect(heading).toHaveAttribute('id', 'test-section-heading');
  });

  it('should have aria-labelledby referencing heading', () => {
    render(
      <SectionGroup id="test-section" label="Group Label" icon="🎯">
        <div>Content</div>
      </SectionGroup>
    );

    const section = document.getElementById('test-section');
    expect(section).toHaveAttribute('aria-labelledby', 'test-section-heading');
  });

  it('should render icon with aria-hidden', () => {
    render(
      <SectionGroup id="test-section" label="Test" icon="🎯">
        <div>Content</div>
      </SectionGroup>
    );

    const iconContainer = screen.getByText('🎯').closest('[role="img"]');
    expect(iconContainer).toHaveAttribute('aria-hidden', 'true');
  });

  it('should render multiple children correctly', () => {
    render(
      <SectionGroup id="test-section" label="Test" icon="🎯">
        <div data-testid="child-1">First child</div>
        <div data-testid="child-2">Second child</div>
        <div data-testid="child-3">Third child</div>
      </SectionGroup>
    );

    expect(screen.getByTestId('child-1')).toBeInTheDocument();
    expect(screen.getByTestId('child-2')).toBeInTheDocument();
    expect(screen.getByTestId('child-3')).toBeInTheDocument();
  });
});
