import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ShareSuccessToast } from './index';

describe('ShareSuccessToast', () => {
  it('renders nothing when visible=false', () => {
    const { container } = render(<ShareSuccessToast visible={false} title="Link copiato" />);
    expect(container.firstChild).toBeNull();
  });

  it('renders title when visible', () => {
    render(<ShareSuccessToast visible title="Link copiato" />);
    expect(screen.getByText('Link copiato')).toBeInTheDocument();
  });

  it('renders subline when provided', () => {
    render(
      <ShareSuccessToast
        visible
        title="Link copiato"
        subline="meepleai.app/s/gn-042 · sparisce in 3s"
      />
    );
    expect(screen.getByText('meepleai.app/s/gn-042 · sparisce in 3s')).toBeInTheDocument();
  });

  it('omits subline when not provided', () => {
    const { container } = render(<ShareSuccessToast visible title="OK" />);
    const monoSpans = container.querySelectorAll('.font-mono');
    expect(monoSpans.length).toBe(0);
  });

  it('uses default 🔗 icon', () => {
    render(<ShareSuccessToast visible title="Link copiato" />);
    expect(screen.getByText('🔗')).toBeInTheDocument();
  });

  it('accepts custom icon override', () => {
    render(<ShareSuccessToast visible title="Archiviato" icon="✓" />);
    expect(screen.getByText('✓')).toBeInTheDocument();
    expect(screen.queryByText('🔗')).toBeNull();
  });

  it('icon is aria-hidden (decorative)', () => {
    render(<ShareSuccessToast visible title="OK" />);
    expect(screen.getByText('🔗')).toHaveAttribute('aria-hidden', 'true');
  });

  it('has role="status" + aria-live="polite" for SR announcements', () => {
    render(<ShareSuccessToast visible title="OK" />);
    const toast = screen.getByRole('status');
    expect(toast).toHaveAttribute('aria-live', 'polite');
    expect(toast).toHaveAttribute('aria-atomic', 'true');
  });

  it('accepts custom className override', () => {
    render(<ShareSuccessToast visible title="OK" className="custom-class" />);
    expect(screen.getByRole('status').className).toContain('custom-class');
  });

  it('uses entity-toolkit color tokens (no hardcoded green)', () => {
    render(<ShareSuccessToast visible title="OK" />);
    const toast = screen.getByRole('status');
    expect(toast.className).toContain('bg-entity-toolkit/10');
    expect(toast.className).toContain('border-entity-toolkit/30');
  });
});
