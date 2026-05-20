import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AutoSaveToast } from './auto-save-toast';

describe('AutoSaveToast', () => {
  it('renders nothing when visible=false', () => {
    const { container } = render(<AutoSaveToast visible={false} timestamp="23:35:42" />);
    expect(container.firstChild).toBeNull();
  });

  it('renders default label "Auto-salvato" when visible', () => {
    render(<AutoSaveToast visible timestamp="23:35:42" />);
    expect(screen.getByText('Auto-salvato')).toBeInTheDocument();
  });

  it('renders timestamp + default 60s next-save in subline', () => {
    render(<AutoSaveToast visible timestamp="23:35:42" />);
    expect(screen.getByText('23:35:42 · prossimo tra 60s')).toBeInTheDocument();
  });

  it('renders custom nextInSeconds in subline', () => {
    render(<AutoSaveToast visible timestamp="23:35:42" nextInSeconds={30} />);
    expect(screen.getByText('23:35:42 · prossimo tra 30s')).toBeInTheDocument();
  });

  it('renders custom label override', () => {
    render(<AutoSaveToast visible timestamp="23:35:42" label="Salvataggio…" />);
    expect(screen.getByText('Salvataggio…')).toBeInTheDocument();
  });

  it('has role="status" and aria-live="polite" for screen reader announcements', () => {
    render(<AutoSaveToast visible timestamp="23:35:42" />);
    const toast = screen.getByRole('status');
    expect(toast).toHaveAttribute('aria-live', 'polite');
    expect(toast).toHaveAttribute('aria-atomic', 'true');
  });

  it('check icon is aria-hidden (decorative)', () => {
    render(<AutoSaveToast visible timestamp="23:35:42" />);
    const checkIcon = screen.getByText('✓');
    expect(checkIcon).toHaveAttribute('aria-hidden', 'true');
  });

  it('accepts custom className override', () => {
    render(<AutoSaveToast visible timestamp="23:35:42" className="custom-class" />);
    const toast = screen.getByRole('status');
    expect(toast.className).toContain('custom-class');
  });

  it('uses entity-toolkit color tokens (no hardcoded green)', () => {
    render(<AutoSaveToast visible timestamp="23:35:42" />);
    const toast = screen.getByRole('status');
    expect(toast.className).toContain('bg-entity-toolkit/10');
    expect(toast.className).toContain('border-entity-toolkit/30');
  });
});
