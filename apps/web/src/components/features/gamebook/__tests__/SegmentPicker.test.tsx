import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { SegmentPicker } from '../SegmentPicker';
import type { GamebookSegment } from '@/lib/api/gamebook-photos';

const segments: GamebookSegment[] = [
  { paragraphNumber: 1, sourceText: 'You stand at the crossroads.', boundingBox: null },
  { paragraphNumber: 3, sourceText: 'The dragon awakens.', boundingBox: '10,20,100,50' },
];

describe('SegmentPicker', () => {
  it('renders empty state when no segments', () => {
    render(<SegmentPicker segments={[]} onPick={vi.fn()} />);
    expect(screen.getByTestId('segment-picker-empty')).toBeInTheDocument();
    expect(screen.queryByTestId('segment-picker')).not.toBeInTheDocument();
  });

  it('renders all segment rows with paragraph numbers and source text', () => {
    render(<SegmentPicker segments={segments} onPick={vi.fn()} />);
    expect(screen.getByTestId('segment-picker')).toBeInTheDocument();
    expect(screen.getByText(/§1/)).toBeInTheDocument();
    expect(screen.getByText(/§3/)).toBeInTheDocument();
    expect(screen.getByText('You stand at the crossroads.')).toBeInTheDocument();
    expect(screen.getByText('The dragon awakens.')).toBeInTheDocument();
  });

  it('calls onPick with the correct paragraphNumber on button click', async () => {
    const onPick = vi.fn();
    render(<SegmentPicker segments={segments} onPick={onPick} />);

    await userEvent.click(screen.getByTestId('segment-picker-translate-3'));

    expect(onPick).toHaveBeenCalledTimes(1);
    expect(onPick).toHaveBeenCalledWith(3);
  });

  it('disables all translate buttons when disabled=true', () => {
    render(<SegmentPicker segments={segments} onPick={vi.fn()} disabled />);
    const buttons = screen.getAllByRole('button', { name: /Traduci/ });
    buttons.forEach(btn => expect(btn).toBeDisabled());
  });
});
