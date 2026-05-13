/**
 * DetailPageLayout — Stage 3 composer primitive (Issue #1026).
 *
 * Tests verify slot ordering, conditional rendering, landmark a11y, and
 * className passthrough. The composer is a pure slot-arranger: tests do not
 * exercise any business logic because the component contains none.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { DetailPageLayout } from './DetailPageLayout';

describe('DetailPageLayout', () => {
  it('renders header and main with only hero + children (other slots omitted)', () => {
    render(
      <DetailPageLayout hero={<span>hero-content</span>}>
        <p>main-content</p>
      </DetailPageLayout>,
    );

    expect(screen.getByRole('banner')).toHaveTextContent('hero-content');
    expect(screen.getByRole('main')).toHaveTextContent('main-content');
    expect(screen.queryByRole('complementary')).not.toBeInTheDocument();
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    expect(screen.queryByRole('contentinfo')).not.toBeInTheDocument();
  });

  it('renders all five landmarks in DOM order: hero → aside → nav → main → footer', () => {
    const { container } = render(
      <DetailPageLayout
        hero={<span data-testid="slot-hero">H</span>}
        connections={<span data-testid="slot-connections">C</span>}
        tabs={<span data-testid="slot-tabs">T</span>}
        footer={<span data-testid="slot-footer">F</span>}
      >
        <span data-testid="slot-main">M</span>
      </DetailPageLayout>,
    );

    const order = Array.from(
      container.querySelectorAll<HTMLElement>('[data-testid^="slot-"]'),
    ).map((node) => node.dataset.testid);

    expect(order).toEqual([
      'slot-hero',
      'slot-connections',
      'slot-tabs',
      'slot-main',
      'slot-footer',
    ]);
  });

  it('omits the <aside> when connections is undefined', () => {
    render(
      <DetailPageLayout hero={<span>H</span>}>
        <p>M</p>
      </DetailPageLayout>,
    );
    expect(screen.queryByRole('complementary')).not.toBeInTheDocument();
  });

  it('omits the <nav> when tabs is undefined', () => {
    render(
      <DetailPageLayout hero={<span>H</span>}>
        <p>M</p>
      </DetailPageLayout>,
    );
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
  });

  it('omits the <footer> when footer is undefined', () => {
    render(
      <DetailPageLayout hero={<span>H</span>}>
        <p>M</p>
      </DetailPageLayout>,
    );
    expect(screen.queryByRole('contentinfo')).not.toBeInTheDocument();
  });
});
