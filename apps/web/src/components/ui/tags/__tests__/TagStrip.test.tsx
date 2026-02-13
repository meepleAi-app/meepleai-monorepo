import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Sparkles, Tag as TagIcon } from 'lucide-react';
import { TagStrip } from '../TagStrip';
import type { Tag } from '@/types/tags';

describe('TagStrip', () => {
  const mockTags: Tag[] = [
    { id: '1', label: 'New', icon: Sparkles, bgColor: 'hsl(142 76% 36%)' },
    { id: '2', label: 'Sale', icon: TagIcon, bgColor: 'hsl(0 84% 60%)' },
    { id: '3', label: 'Owned', bgColor: 'hsl(221 83% 53%)' },
    { id: '4', label: 'Wishlist', bgColor: 'hsl(350 89% 60%)' }
  ];

  it('renders maximum visible tags', () => {
    render(<TagStrip tags={mockTags} maxVisible={3} />);
    expect(screen.getByText('New')).toBeInTheDocument();
    expect(screen.getByText('Sale')).toBeInTheDocument();
    expect(screen.getByText('Owned')).toBeInTheDocument();
  });

  it('shows overflow counter', () => {
    render(<TagStrip tags={mockTags} maxVisible={3} />);
    expect(screen.getByText('+1')).toBeInTheDocument();
  });

  it('renders nothing when empty', () => {
    const { container } = render(<TagStrip tags={[]} />);
    expect(container.firstChild).toBeNull();
  });
});
