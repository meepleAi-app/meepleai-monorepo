import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ArchivedBanner } from './index';

describe('ArchivedBanner', () => {
  it('renders title (kicker)', () => {
    render(<ArchivedBanner title="Serata archiviata" />);
    expect(screen.getByText('Serata archiviata')).toBeInTheDocument();
  });

  it('renders subtitle when provided', () => {
    render(
      <ArchivedBanner
        title="Serata archiviata"
        subtitle="Riepilogo salvato · accessibile da /game-nights/archived"
      />
    );
    expect(
      screen.getByText('Riepilogo salvato · accessibile da /game-nights/archived')
    ).toBeInTheDocument();
  });

  it('omits subtitle div when not provided', () => {
    const { container } = render(<ArchivedBanner title="OK" />);
    expect(container.querySelector('.font-display')).toBeNull();
  });

  it('uses default 📦 icon', () => {
    render(<ArchivedBanner title="OK" />);
    expect(screen.getByText('📦')).toBeInTheDocument();
  });

  it('accepts custom icon override', () => {
    render(<ArchivedBanner title="OK" icon="📁" />);
    expect(screen.getByText('📁')).toBeInTheDocument();
    expect(screen.queryByText('📦')).toBeNull();
  });

  it('icon is aria-hidden (decorative)', () => {
    render(<ArchivedBanner title="OK" />);
    expect(screen.getByText('📦')).toHaveAttribute('aria-hidden', 'true');
  });

  it('renders action slot when provided', () => {
    render(<ArchivedBanner title="OK" action={<a href="/game-nights">← Torna alla lista</a>} />);
    expect(screen.getByRole('link', { name: '← Torna alla lista' })).toBeInTheDocument();
  });

  it('omits action wrapper when not provided', () => {
    const { container } = render(<ArchivedBanner title="OK" />);
    // expect only 2 top-level children: icon span + content div (no action div)
    expect(container.firstChild?.childNodes.length).toBe(2);
  });

  it('has role="status" (non-critical announcement)', () => {
    render(<ArchivedBanner title="OK" />);
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('uses muted neutral (no entity color) as per brief', () => {
    const { container } = render(<ArchivedBanner title="OK" />);
    const banner = container.firstChild as HTMLElement;
    expect(banner.className).toContain('bg-muted');
    expect(banner.className).not.toContain('entity-event');
    expect(banner.className).not.toContain('entity-toolkit');
  });

  it('accepts custom className override', () => {
    render(<ArchivedBanner title="OK" className="custom-class" />);
    expect(screen.getByRole('status').className).toContain('custom-class');
  });
});
