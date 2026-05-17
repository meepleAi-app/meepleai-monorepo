import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ToggleSwitch } from './toggle-switch';

describe('ToggleSwitch', () => {
  // Silence the dev warning for tests that intentionally omit aria labels
  let warnSpy: ReturnType<typeof vi.spyOn>;
  beforeEach(() => {
    warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
  });
  afterEach(() => {
    warnSpy.mockRestore();
  });

  it('renders with role="switch"', () => {
    render(<ToggleSwitch checked={false} onCheckedChange={() => {}} ariaLabel="Toggle" />);
    expect(screen.getByRole('switch')).toBeInTheDocument();
  });

  it('aria-checked reflects the checked prop (false)', () => {
    render(<ToggleSwitch checked={false} onCheckedChange={() => {}} ariaLabel="Off" />);
    expect(screen.getByRole('switch')).toHaveAttribute('aria-checked', 'false');
  });

  it('aria-checked reflects the checked prop (true)', () => {
    render(<ToggleSwitch checked onCheckedChange={() => {}} ariaLabel="On" />);
    expect(screen.getByRole('switch')).toHaveAttribute('aria-checked', 'true');
  });

  it('fires onCheckedChange with the opposite value when clicked', async () => {
    const onCheckedChange = vi.fn();
    render(<ToggleSwitch checked={false} onCheckedChange={onCheckedChange} ariaLabel="T" />);
    await userEvent.click(screen.getByRole('switch'));
    expect(onCheckedChange).toHaveBeenCalledTimes(1);
    expect(onCheckedChange).toHaveBeenCalledWith(true);
  });

  it('does not fire onCheckedChange when disabled', async () => {
    const onCheckedChange = vi.fn();
    render(
      <ToggleSwitch checked={false} onCheckedChange={onCheckedChange} disabled ariaLabel="T" />
    );
    await userEvent.click(screen.getByRole('switch'));
    expect(onCheckedChange).not.toHaveBeenCalled();
  });

  it('active state sets backgroundColor inline via entity color', () => {
    render(<ToggleSwitch checked onCheckedChange={() => {}} entity="game" ariaLabel="T" />);
    const sw = screen.getByRole('switch');
    expect(sw.getAttribute('style')).toMatch(/--e-game/);
  });

  it('default entity is "game"', () => {
    render(<ToggleSwitch checked onCheckedChange={() => {}} ariaLabel="T" />);
    expect(screen.getByRole('switch')).toHaveAttribute('data-entity', 'game');
  });

  it('kb entity maps to --e-document CSS var', () => {
    render(<ToggleSwitch checked onCheckedChange={() => {}} entity="kb" ariaLabel="T" />);
    const sw = screen.getByRole('switch');
    expect(sw.getAttribute('style')).toMatch(/--e-document/);
  });

  it('size "sm" uses smaller track dimensions', () => {
    render(<ToggleSwitch checked={false} onCheckedChange={() => {}} size="sm" ariaLabel="T" />);
    const sw = screen.getByRole('switch');
    expect(sw.className).toMatch(/w-8/);
    expect(sw.className).toMatch(/h-5/);
  });

  it('size "md" is the default', () => {
    render(<ToggleSwitch checked={false} onCheckedChange={() => {}} ariaLabel="T" />);
    const sw = screen.getByRole('switch');
    expect(sw.className).toMatch(/w-10/);
    expect(sw.className).toMatch(/h-6/);
  });

  it('forwards ariaLabel to the root', () => {
    render(<ToggleSwitch checked={false} onCheckedChange={() => {}} ariaLabel="Notifications" />);
    expect(screen.getByRole('switch', { name: 'Notifications' })).toBeInTheDocument();
  });

  it('forwards ariaLabelledBy to the root', () => {
    render(
      <>
        <span id="lbl">2FA enabled</span>
        <ToggleSwitch checked={false} onCheckedChange={() => {}} ariaLabelledBy="lbl" />
      </>
    );
    const sw = screen.getByRole('switch');
    expect(sw).toHaveAttribute('aria-labelledby', 'lbl');
  });

  it('merges className', () => {
    render(
      <ToggleSwitch
        checked={false}
        onCheckedChange={() => {}}
        className="my-switch"
        ariaLabel="T"
      />
    );
    expect(screen.getByRole('switch')).toHaveClass('my-switch');
  });

  it('Space key triggers onCheckedChange (native button)', async () => {
    const onCheckedChange = vi.fn();
    render(<ToggleSwitch checked={false} onCheckedChange={onCheckedChange} ariaLabel="T" />);
    const sw = screen.getByRole('switch');
    sw.focus();
    await userEvent.keyboard(' ');
    expect(onCheckedChange).toHaveBeenCalledWith(true);
  });

  it('is disabled via the disabled attribute and aria-disabled', () => {
    render(<ToggleSwitch checked={false} onCheckedChange={() => {}} disabled ariaLabel="T" />);
    const sw = screen.getByRole('switch');
    expect(sw).toBeDisabled();
    expect(sw).toHaveAttribute('aria-disabled', 'true');
  });

  it('forwards id', () => {
    render(<ToggleSwitch checked={false} onCheckedChange={() => {}} id="toggle-1" ariaLabel="T" />);
    expect(screen.getByRole('switch')).toHaveAttribute('id', 'toggle-1');
  });
});
