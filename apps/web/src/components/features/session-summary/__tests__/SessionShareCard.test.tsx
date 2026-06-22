/**
 * SessionShareCard unit tests — Wave D.3 (Issue #756).
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { RankedParticipant } from '@/lib/sessions-summary/tie-groups';

import { SessionShareCard } from '../SessionShareCard';
import type { SessionShareCardProps } from '../SessionShareCard';

const PODIUM: RankedParticipant[] = [
  {
    id: 'p1',
    userId: 'u1',
    displayName: 'Marco',
    isOwner: true,
    joinOrder: 1,
    finalRank: 1,
    totalScore: 89,
    rank: 1,
    isTied: false,
    tiedPlayerIds: ['p1'],
  },
  {
    id: 'p2',
    userId: 'u2',
    displayName: 'Anna',
    isOwner: false,
    joinOrder: 2,
    finalRank: 2,
    totalScore: 79,
    rank: 2,
    isTied: false,
    tiedPlayerIds: ['p2'],
  },
  {
    id: 'p3',
    userId: 'u3',
    displayName: 'Luca',
    isOwner: false,
    joinOrder: 3,
    finalRank: 3,
    totalScore: 64,
    rank: 3,
    isTied: false,
    tiedPlayerIds: ['p3'],
  },
];

const LABELS: SessionShareCardProps['labels'] = {
  title: 'Share card preview',
  previewLight: 'Light',
  previewDark: 'Dark',
  shareTwitter: 'Twitter',
  shareInstagram: 'Instagram',
  shareWhatsApp: 'WhatsApp',
  copyLink: 'Copia link',
  downloadPng: 'Download PNG',
  downloadPngDisabled: 'In arrivo prossimamente',
  previewHeadline: 'Marco vince Wingspan',
  previewMeta: '23 apr 2026 · 1h 24min · 4 giocatori',
};

const DEFAULT_PROPS: SessionShareCardProps = {
  podium: PODIUM,
  theme: 'light',
  onThemeChange: vi.fn(),
  onShare: vi.fn(),
  labels: LABELS,
};

describe('SessionShareCard', () => {
  it('renders data-slot="session-share-card"', () => {
    render(<SessionShareCard {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="session-share-card"]')).not.toBeNull();
  });

  it('renders preview headline + meta', () => {
    render(<SessionShareCard {...DEFAULT_PROPS} />);
    expect(screen.getByText('Marco vince Wingspan')).toBeTruthy();
    expect(screen.getByText('23 apr 2026 · 1h 24min · 4 giocatori')).toBeTruthy();
  });

  it('marks data-theme="light" by default', () => {
    render(<SessionShareCard {...DEFAULT_PROPS} />);
    const root = document.querySelector('[data-slot="session-share-card"]')!;
    expect(root.getAttribute('data-theme')).toBe('light');
  });

  it('marks data-theme="dark" when theme prop is dark', () => {
    render(<SessionShareCard {...DEFAULT_PROPS} theme="dark" />);
    const root = document.querySelector('[data-slot="session-share-card"]')!;
    expect(root.getAttribute('data-theme')).toBe('dark');
  });

  it('renders theme toggle radiogroup with 2 options', () => {
    render(<SessionShareCard {...DEFAULT_PROPS} />);
    expect(document.querySelectorAll('[data-slot="share-theme-option"]').length).toBe(2);
  });

  it('marks active theme with aria-checked + data-active', () => {
    render(<SessionShareCard {...DEFAULT_PROPS} theme="light" />);
    const lightOpt = document.querySelector('[data-theme-option="light"]')!;
    const darkOpt = document.querySelector('[data-theme-option="dark"]')!;
    expect(lightOpt.getAttribute('aria-checked')).toBe('true');
    expect(lightOpt.getAttribute('data-active')).toBe('true');
    expect(darkOpt.getAttribute('aria-checked')).toBe('false');
    expect(darkOpt.getAttribute('data-active')).toBeNull();
  });

  it('fires onThemeChange when toggle is clicked', () => {
    const onThemeChange = vi.fn();
    render(<SessionShareCard {...DEFAULT_PROPS} onThemeChange={onThemeChange} />);
    const darkOpt = document.querySelector('[data-theme-option="dark"]') as HTMLButtonElement;
    fireEvent.click(darkOpt);
    expect(onThemeChange).toHaveBeenCalledWith('dark');
  });

  it('renders 5 share buttons (twitter/insta/whatsapp/copy/png-disabled)', () => {
    render(<SessionShareCard {...DEFAULT_PROPS} />);
    expect(document.querySelectorAll('[data-slot="share-button"]').length).toBe(5);
  });

  it('PNG button is disabled with title tooltip', () => {
    render(<SessionShareCard {...DEFAULT_PROPS} />);
    const png = document.querySelector('[data-channel="png"]') as HTMLButtonElement;
    expect(png.disabled).toBe(true);
    expect(png.getAttribute('aria-disabled')).toBe('true');
    expect(png.getAttribute('title')).toBe('In arrivo prossimamente');
    expect(png.getAttribute('data-disabled')).toBe('true');
  });

  it('fires onShare with correct channel for twitter button', () => {
    const onShare = vi.fn();
    render(<SessionShareCard {...DEFAULT_PROPS} onShare={onShare} />);
    fireEvent.click(document.querySelector('[data-channel="twitter"]') as HTMLButtonElement);
    expect(onShare).toHaveBeenCalledWith('twitter');
  });

  it('fires onShare with correct channel for copy button', () => {
    const onShare = vi.fn();
    render(<SessionShareCard {...DEFAULT_PROPS} onShare={onShare} />);
    fireEvent.click(document.querySelector('[data-channel="copy"]') as HTMLButtonElement);
    expect(onShare).toHaveBeenCalledWith('copy');
  });

  it('renders 3 podium cells in preview when 3 participants provided', () => {
    render(<SessionShareCard {...DEFAULT_PROPS} />);
    expect(document.querySelectorAll('[data-slot="share-preview-podium-cell"]').length).toBe(3);
  });

  it('renders podium cells in [2nd, 1st, 3rd] order', () => {
    render(<SessionShareCard {...DEFAULT_PROPS} />);
    const cells = document.querySelectorAll('[data-slot="share-preview-podium-cell"]');
    expect(cells[0].getAttribute('data-place')).toBe('2');
    expect(cells[1].getAttribute('data-place')).toBe('1');
    expect(cells[2].getAttribute('data-place')).toBe('3');
  });

  it('uses role="radiogroup" on theme toggle container', () => {
    render(<SessionShareCard {...DEFAULT_PROPS} />);
    const radiogroup = document.querySelector('[role="radiogroup"]');
    expect(radiogroup).not.toBeNull();
  });

  it('preview surface marks data-preview-theme matching theme prop', () => {
    render(<SessionShareCard {...DEFAULT_PROPS} theme="dark" />);
    const preview = document.querySelector('[data-slot="share-preview"]')!;
    expect(preview.getAttribute('data-preview-theme')).toBe('dark');
  });
});
