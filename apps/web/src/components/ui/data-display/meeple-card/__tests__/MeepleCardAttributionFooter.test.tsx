import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { MeepleCardAttributionFooter } from '../MeepleCardAttributionFooter';

describe('MeepleCardAttributionFooter — Issue #2055 Phase G AC-G6', () => {
  it('returns null when license is null', () => {
    const { container } = render(
      <MeepleCardAttributionFooter
        license={null}
        attribution="Klaus"
        sourceUrl="https://commons.wikimedia.org/x"
      />
    );
    expect(container.firstChild).toBeNull();
  });

  it('renders license only when attribution + sourceUrl are null', () => {
    const { container, getByText } = render(
      <MeepleCardAttributionFooter license="CC-BY-SA 4.0" attribution={null} sourceUrl={null} />
    );
    expect(getByText('CC-BY-SA 4.0')).toBeInTheDocument();
    expect(container.querySelector('a')).toBeNull();
  });

  it('renders license + attribution + source link when all provided', () => {
    const { getByText, getByRole } = render(
      <MeepleCardAttributionFooter
        license="CC-BY-SA 4.0"
        attribution="Klaus Teuber"
        sourceUrl="https://commons.wikimedia.org/wiki/File:Catan.jpg"
      />
    );
    expect(getByText('CC-BY-SA 4.0')).toBeInTheDocument();
    expect(getByText('Klaus Teuber')).toBeInTheDocument();
    const link = getByRole('link', { name: 'source' });
    expect(link).toHaveAttribute('href', 'https://commons.wikimedia.org/wiki/File:Catan.jpg');
    expect(link).toHaveAttribute('rel', 'nofollow noopener noreferrer');
    expect(link).toHaveAttribute('target', '_blank');
  });

  it('renders attribution as PLAIN TEXT (no HTML execution) — XSS defense', () => {
    // BE strips HTML upstream; this verifies React default escaping is also active
    // in case a buggy BE skips Strip. Issue #2055 DEC-G6-1.
    const malicious = '<script>alert(1)</script>Klaus';
    const { container } = render(
      <MeepleCardAttributionFooter
        license="CC-BY-SA 4.0"
        attribution={malicious}
        sourceUrl={null}
      />
    );
    // React escapes < and > automatically; the literal string should appear
    expect(container.textContent).toContain('<script>alert(1)</script>Klaus');
    // No <script> element actually rendered
    expect(container.querySelector('script')).toBeNull();
  });
});
