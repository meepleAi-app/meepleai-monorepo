import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SegmentedControl } from './SegmentedControl';

const segments = [
  { id: 'collection', label: 'Collezione' },
  { id: 'private', label: 'Privati' },
  { id: 'wishlist', label: 'Wishlist' },
];

describe('SegmentedControl', () => {
  it('renders all segments', () => {
    render(<SegmentedControl segments={segments} activeId="collection" onChange={() => {}} />);
    expect(screen.getByText('Collezione')).toBeInTheDocument();
    expect(screen.getByText('Privati')).toBeInTheDocument();
    expect(screen.getByText('Wishlist')).toBeInTheDocument();
  });

  it('highlights active segment', () => {
    render(<SegmentedControl segments={segments} activeId="private" onChange={() => {}} />);
    const btn = screen.getByText('Privati').closest('button');
    expect(btn).toHaveAttribute('aria-selected', 'true');
  });

  it('calls onChange when segment is clicked', () => {
    const onChange = vi.fn();
    render(<SegmentedControl segments={segments} activeId="collection" onChange={onChange} />);
    fireEvent.click(screen.getByText('Wishlist'));
    expect(onChange).toHaveBeenCalledWith('wishlist');
  });

  it('has tablist role', () => {
    const { container } = render(
      <SegmentedControl segments={segments} activeId="collection" onChange={() => {}} />
    );
    expect(container.firstChild).toHaveAttribute('role', 'tablist');
  });
});
