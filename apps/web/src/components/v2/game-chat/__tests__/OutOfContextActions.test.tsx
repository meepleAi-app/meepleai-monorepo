import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { OutOfContextActions } from '../OutOfContextActions';

describe('OutOfContextActions', () => {
  it('renders all action pills with icons', () => {
    const onClick = vi.fn();
    render(
      <OutOfContextActions
        actions={[
          { kind: 'switch-game', label: 'Cambia gioco a Tainted Grail', onClick },
          { kind: 'find-agent', label: 'Cerca un agente Tainted Grail', onClick },
          { kind: 'stay', label: 'Resta su Wingspan', onClick },
        ]}
      />
    );
    expect(screen.getByRole('button', { name: /Cambia gioco/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Cerca un agente/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Resta su Wingspan/ })).toBeInTheDocument();
  });

  it('calls correct onClick when each action clicked', () => {
    const a = vi.fn(); const b = vi.fn(); const c = vi.fn();
    render(
      <OutOfContextActions
        actions={[
          { kind: 'switch-game', label: 'A', onClick: a },
          { kind: 'find-agent', label: 'B', onClick: b },
          { kind: 'stay', label: 'C', onClick: c },
        ]}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /A/ }));
    expect(a).toHaveBeenCalledOnce();
    expect(b).not.toHaveBeenCalled();
    expect(c).not.toHaveBeenCalled();
  });

  it('renders nothing when actions empty', () => {
    const { container } = render(<OutOfContextActions actions={[]} />);
    expect(container.firstChild).toBeNull();
  });
});
