/**
 * Wave A.4 (Issue #603) — ToolkitListItem rendering tests.
 *
 * Verifies the published toolkit row contract from spec §3.3:
 *  - Title + author meta + last-updated date
 *  - previewHref present → enabled <a> with aria-label
 *  - previewHref absent → disabled <span aria-disabled="true">
 *  - data-slot + data-toolkit-id attributes
 *  - Optional description rendering
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ToolkitListItem } from './toolkit-list-item';

const labels = {
  authorPrefix: 'by',
  updatedPrefix: 'Updated',
  previewLabel: 'Anteprima',
  previewAriaLabel: (name: string) => `Preview toolkit ${name}`,
};

describe('ToolkitListItem (Wave A.4)', () => {
  it('renders name, owner, and updated meta', () => {
    render(
      <ToolkitListItem
        id="t1"
        name="StarterKit"
        ownerName="Alice"
        lastUpdatedAt="2026-04-15T00:00:00Z"
        labels={labels}
      />
    );
    expect(screen.getByRole('heading', { level: 3, name: 'StarterKit' })).toBeInTheDocument();
    expect(screen.getByText(/by/)).toBeInTheDocument();
    expect(screen.getByText(/Alice/)).toBeInTheDocument();
    expect(screen.getByText(/Updated/)).toBeInTheDocument();
  });

  it('renders <time> element with dateTime attribute', () => {
    const { container } = render(
      <ToolkitListItem
        id="t1"
        name="StarterKit"
        ownerName="Alice"
        lastUpdatedAt="2026-04-15T00:00:00Z"
        labels={labels}
      />
    );
    const time = container.querySelector('time');
    expect(time).toHaveAttribute('datetime', '2026-04-15T00:00:00Z');
  });

  it('renders enabled link when previewHref is provided', () => {
    render(
      <ToolkitListItem
        id="t1"
        name="StarterKit"
        ownerName="Alice"
        lastUpdatedAt="2026-04-15T00:00:00Z"
        previewHref="/toolkits/t1"
        labels={labels}
      />
    );
    const link = screen.getByRole('link', { name: 'Preview toolkit StarterKit' });
    expect(link).toHaveAttribute('href', '/toolkits/t1');
  });

  it('renders disabled span fallback when previewHref is omitted', () => {
    const { container } = render(
      <ToolkitListItem
        id="t1"
        name="StarterKit"
        ownerName="Alice"
        lastUpdatedAt="2026-04-15T00:00:00Z"
        labels={labels}
      />
    );
    expect(container.querySelector('a')).toBeNull();
    const disabled = container.querySelector('[aria-disabled="true"]');
    expect(disabled).not.toBeNull();
    expect(disabled?.textContent).toBe('Anteprima');
  });

  it('renders optional description', () => {
    render(
      <ToolkitListItem
        id="t1"
        name="StarterKit"
        ownerName="Alice"
        lastUpdatedAt="2026-04-15T00:00:00Z"
        description="Boilerplate toolkit for new games"
        labels={labels}
      />
    );
    expect(screen.getByText('Boilerplate toolkit for new games')).toBeInTheDocument();
  });

  it('does not render description paragraph when omitted', () => {
    const { container } = render(
      <ToolkitListItem
        id="t1"
        name="StarterKit"
        ownerName="Alice"
        lastUpdatedAt="2026-04-15T00:00:00Z"
        labels={labels}
      />
    );
    expect(container.querySelectorAll('p')).toHaveLength(1);
  });

  it('exposes data-slot and data-toolkit-id attributes', () => {
    const { container } = render(
      <ToolkitListItem
        id="toolkit-xyz"
        name="StarterKit"
        ownerName="Alice"
        lastUpdatedAt="2026-04-15T00:00:00Z"
        labels={labels}
      />
    );
    const root = container.querySelector('[data-slot="shared-game-detail-toolkit-item"]');
    expect(root).not.toBeNull();
    expect(root).toHaveAttribute('data-toolkit-id', 'toolkit-xyz');
  });

  it('passes through optional className', () => {
    const { container } = render(
      <ToolkitListItem
        id="t1"
        name="StarterKit"
        ownerName="Alice"
        lastUpdatedAt="2026-04-15T00:00:00Z"
        labels={labels}
        className="custom-cls"
      />
    );
    const root = container.querySelector('[data-slot="shared-game-detail-toolkit-item"]');
    expect(root?.className).toContain('custom-cls');
  });
});
