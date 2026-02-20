/**
 * Tests for MediaTab component
 * Issue #4762 - ExtraMeepleCard: Media Tab + AI Tab + Other Entity Types
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { MediaTab } from '../tabs/MediaTab';
import type { MediaTabData, MediaItem } from '../types';

// ============================================================================
// Test Data
// ============================================================================

const mockPhotos: MediaItem[] = [
  {
    id: 'photo-1',
    type: 'photo',
    url: 'https://example.com/photo1.jpg',
    thumbnailUrl: 'https://example.com/thumb1.jpg',
    title: 'Board setup',
    turnNumber: 1,
    createdAt: '2026-02-19T10:00:00Z',
    createdBy: 'Alice',
  },
  {
    id: 'photo-2',
    type: 'photo',
    url: 'https://example.com/photo2.jpg',
    title: 'Final board state',
    turnNumber: 5,
    createdAt: '2026-02-19T11:00:00Z',
  },
];

const mockNotes: MediaItem[] = [
  {
    id: 'note-1',
    type: 'note',
    title: 'Strategy notes',
    content: 'Focus on ore and wheat production',
    turnNumber: 3,
    createdAt: '2026-02-19T10:30:00Z',
    createdBy: 'Bob',
  },
];

const mockMediaData: MediaTabData = {
  items: [...mockPhotos, ...mockNotes],
  totalPhotos: 2,
  totalNotes: 1,
};

// ============================================================================
// Tests
// ============================================================================

describe('MediaTab', () => {
  // --- Empty states ---

  it('renders empty state when no data', () => {
    render(<MediaTab />);

    expect(screen.getByText('No media yet')).toBeInTheDocument();
    expect(screen.getByText('Photos and notes will appear here')).toBeInTheDocument();
  });

  it('renders empty filtered state when no items match filter', () => {
    const emptyPhotosData: MediaTabData = {
      items: mockNotes,
      totalPhotos: 0,
      totalNotes: 1,
    };
    render(<MediaTab data={emptyPhotosData} />);

    fireEvent.click(screen.getByTestId('media-filter-photos'));
    expect(screen.getByText('No photos found')).toBeInTheDocument();
  });

  // --- Photo rendering ---

  it('renders photos with thumbnails', () => {
    render(<MediaTab data={mockMediaData} />);

    const photo1 = screen.getByTestId('media-photo-photo-1');
    expect(photo1).toBeInTheDocument();
    const img = photo1.querySelector('img');
    expect(img).toHaveAttribute('src', 'https://example.com/thumb1.jpg');
  });

  it('renders turn number badges on photos', () => {
    render(<MediaTab data={mockMediaData} />);

    expect(screen.getByText('T1')).toBeInTheDocument();
    expect(screen.getByText('T5')).toBeInTheDocument();
  });

  // --- Note rendering ---

  it('renders notes with title and content', () => {
    render(<MediaTab data={mockMediaData} />);

    expect(screen.getByText('Strategy notes')).toBeInTheDocument();
    expect(screen.getByText('Focus on ore and wheat production')).toBeInTheDocument();
  });

  it('renders note turn number and author', () => {
    render(<MediaTab data={mockMediaData} />);

    const noteEl = screen.getByTestId('media-note-note-1');
    expect(noteEl).toBeInTheDocument();
    expect(screen.getByText('Turn 3')).toBeInTheDocument();
  });

  // --- Filter system ---

  it('renders filter buttons with counts', () => {
    render(<MediaTab data={mockMediaData} />);

    expect(screen.getByTestId('media-filter-all')).toBeInTheDocument();
    expect(screen.getByTestId('media-filter-photos')).toBeInTheDocument();
    expect(screen.getByTestId('media-filter-notes')).toBeInTheDocument();
    expect(screen.getByText('Photos (2)')).toBeInTheDocument();
    expect(screen.getByText('Notes (1)')).toBeInTheDocument();
  });

  it('filters to photos only', () => {
    render(<MediaTab data={mockMediaData} />);

    fireEvent.click(screen.getByTestId('media-filter-photos'));

    expect(screen.getByTestId('media-photo-photo-1')).toBeInTheDocument();
    expect(screen.getByTestId('media-photo-photo-2')).toBeInTheDocument();
    expect(screen.queryByTestId('media-note-note-1')).not.toBeInTheDocument();
  });

  it('filters to notes only', () => {
    render(<MediaTab data={mockMediaData} />);

    fireEvent.click(screen.getByTestId('media-filter-notes'));

    expect(screen.getByTestId('media-note-note-1')).toBeInTheDocument();
    expect(screen.queryByTestId('media-photo-photo-1')).not.toBeInTheDocument();
  });

  it('shows all items with "all" filter', () => {
    render(<MediaTab data={mockMediaData} />);

    // Default is "all"
    expect(screen.getByTestId('media-photo-photo-1')).toBeInTheDocument();
    expect(screen.getByTestId('media-note-note-1')).toBeInTheDocument();
  });

  // --- Section headings ---

  it('renders section headings in "all" filter mode', () => {
    render(<MediaTab data={mockMediaData} />);

    expect(screen.getByText('Photos')).toBeInTheDocument();
    expect(screen.getByText('Notes')).toBeInTheDocument();
  });

  it('hides section headings in single-type filter mode', () => {
    render(<MediaTab data={mockMediaData} />);

    fireEvent.click(screen.getByTestId('media-filter-photos'));

    // Section heading "Photos" should not appear when filtering photos only
    expect(screen.queryByText('Photos')).not.toBeInTheDocument();
  });

  // --- Photo without thumbnail ---

  it('renders photo fallback when no url or thumbnail', () => {
    const noUrlPhoto: MediaTabData = {
      items: [{ id: 'p-no-url', type: 'photo', createdAt: '2026-02-19T10:00:00Z' }],
      totalPhotos: 1,
      totalNotes: 0,
    };
    render(<MediaTab data={noUrlPhoto} />);

    const photoEl = screen.getByTestId('media-photo-p-no-url');
    expect(photoEl).toBeInTheDocument();
    // Should not have an img tag (fallback icon rendered instead)
    expect(photoEl.querySelector('img')).toBeNull();
  });
});
