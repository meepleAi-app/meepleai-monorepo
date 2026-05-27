import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { BackupCodesView } from '../two-factor/BackupCodesView';

const CODES = ['AAAA-1111', 'BBBB-2222', 'CCCC-3333', 'DDDD-4444'];

describe('BackupCodesView', () => {
  beforeEach(() => {
    // Reset clipboard / URL mocks per test
    Object.assign(navigator, { clipboard: { writeText: vi.fn().mockResolvedValue(undefined) } });
    Object.assign(URL, {
      createObjectURL: vi.fn(() => 'blob:mock-url'),
      revokeObjectURL: vi.fn(),
    });
  });

  it('renders all codes', () => {
    render(<BackupCodesView codes={CODES} acked={false} onAck={() => {}} />);
    CODES.forEach(c => expect(screen.getByText(c)).toBeInTheDocument());
  });

  it('toggles ack via the checkbox', () => {
    const onAck = vi.fn();
    render(<BackupCodesView codes={CODES} acked={false} onAck={onAck} />);
    fireEvent.click(screen.getByRole('checkbox'));
    expect(onAck).toHaveBeenCalledWith(true);
  });

  it('reflects the acked prop on the checkbox', () => {
    const { rerender } = render(<BackupCodesView codes={CODES} acked={false} onAck={() => {}} />);
    expect(screen.getByRole('checkbox')).not.toBeChecked();
    rerender(<BackupCodesView codes={CODES} acked={true} onAck={() => {}} />);
    expect(screen.getByRole('checkbox')).toBeChecked();
  });

  it('copies all codes to clipboard on "Copy all" (G1)', async () => {
    render(<BackupCodesView codes={CODES} acked={false} onAck={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: /copy all/i }));
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith(CODES.join('\n'));
  });

  it('triggers download on "Download .txt" (G1)', () => {
    render(<BackupCodesView codes={CODES} acked={false} onAck={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: /download/i }));
    expect(URL.createObjectURL).toHaveBeenCalled();
  });
});
