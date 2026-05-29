import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { KbDocDetailTabs } from '../KbDocDetailTabs';

vi.mock('next/navigation', async () => {
  const actual = await vi.importActual<typeof import('next/navigation')>('next/navigation');
  return {
    ...actual,
    usePathname: () => '/admin/knowledge-base',
    useSearchParams: () => new URLSearchParams('docId=abc&tab=ingestion'),
  };
});

describe('KbDocDetailTabs', () => {
  it('renders two tabs: Overview and Ingestion log', () => {
    render(<KbDocDetailTabs docId="abc" activeTab="overview" />);
    expect(screen.getByRole('link', { name: /overview/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /ingestion log/i })).toBeInTheDocument();
  });

  it('preserves docId in each tab href', () => {
    render(<KbDocDetailTabs docId="abc" activeTab="overview" />);
    const ingestionLink = screen.getByRole('link', { name: /ingestion log/i });
    expect(ingestionLink.getAttribute('href')).toContain('docId=abc');
    expect(ingestionLink.getAttribute('href')).toContain('tab=ingestion');
  });

  it('marks active tab with aria-current="page"', () => {
    render(<KbDocDetailTabs docId="abc" activeTab="ingestion" />);
    expect(screen.getByRole('link', { name: /ingestion log/i }).getAttribute('aria-current')).toBe(
      'page'
    );
    expect(screen.getByRole('link', { name: /overview/i }).getAttribute('aria-current')).toBeNull();
  });
});
