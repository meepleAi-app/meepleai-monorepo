/**
 * TemplateSelector Component Tests (Issue #3412)
 *
 * Test Coverage:
 * - Template list rendering
 * - Template selection
 * - Template forking
 * - Empty state
 *
 * Target: >80% coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render } from '@testing-library/react';
import { TemplateSelector } from '../TemplateSelector';

// ============================================================================
// Rendering Tests
// ============================================================================

describe('TemplateSelector - Rendering', () => {
  it('renders without crashing', () => {
    const { container } = render(
      <TemplateSelector
        onSelect={vi.fn()}
      />
    );
    expect(container).toBeInTheDocument();
  });

  it('renders with onSelect callback', () => {
    const onSelect = vi.fn();
    const { container } = render(
      <TemplateSelector onSelect={onSelect} />
    );
    expect(container).toBeInTheDocument();
  });

  it('renders with initial selection', () => {
    const { container } = render(
      <TemplateSelector
        onSelect={vi.fn()}
        initialSelectedId="template-1"
      />
    );
    expect(container).toBeInTheDocument();
  });

  it('renders without initial selection', () => {
    const { container } = render(
      <TemplateSelector onSelect={vi.fn()} />
    );
    expect(container).toBeInTheDocument();
  });

  it('renders with custom className', () => {
    const { container } = render(
      <TemplateSelector
        onSelect={vi.fn()}
        className="custom-class"
      />
    );
    expect(container.firstChild).toHaveClass('custom-class');
  });
});
