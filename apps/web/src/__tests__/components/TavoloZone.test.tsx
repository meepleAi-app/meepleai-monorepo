import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { TavoloZone } from '@/components/dashboard/TavoloZone';

describe('TavoloZone', () => {
  it('renders children with env-tavolo class', () => {
    render(
      <TavoloZone>
        <div>Inside tavolo</div>
      </TavoloZone>
    );
    const zone = screen.getByTestId('tavolo-zone');
    expect(zone.className).toContain('env-tavolo');
    expect(screen.getByText('Inside tavolo')).toBeDefined();
  });

  it('returns null when isEmpty is true', () => {
    render(
      <TavoloZone isEmpty>
        <div>Hidden</div>
      </TavoloZone>
    );
    expect(screen.queryByTestId('tavolo-zone')).toBeNull();
  });

  it('applies additional className', () => {
    render(
      <TavoloZone className="mt-4">
        <div>Content</div>
      </TavoloZone>
    );
    const zone = screen.getByTestId('tavolo-zone');
    expect(zone.className).toContain('mt-4');
  });
});
