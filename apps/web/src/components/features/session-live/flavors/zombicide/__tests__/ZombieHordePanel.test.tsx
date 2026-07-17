import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ZombieHordePanel } from '../ZombieHordePanel';

const zombies = { walker: 5, runner: 2, fatty: 0, berserker: 1, abomination: 0, necromancer: 1 };
const labels = {
  heading: 'Orda',
  walker: 'Camminatore',
  runner: 'Corridore',
  fatty: 'Grasso',
  berserker: 'Berserker',
  abomination: 'Abominio',
  necromancer: 'Negromante',
  incAria: '{field} +1',
  decAria: '{field} -1',
};

describe('ZombieHordePanel', () => {
  it('renders all 6 zombie types with counts', () => {
    const { container } = render(
      <ZombieHordePanel zombies={zombies} editable={false} labels={labels} />
    );
    expect(container.querySelectorAll('[data-zombie]')).toHaveLength(6);
    expect(screen.getByText('Camminatore').closest('[data-zombie]')?.textContent).toContain('5');
  });

  it('read-only exposes no steppers', () => {
    render(<ZombieHordePanel zombies={zombies} editable={false} labels={labels} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host: walker +1 fires onBump', async () => {
    const onBump = vi.fn();
    render(<ZombieHordePanel zombies={zombies} editable onBump={onBump} labels={labels} />);
    await userEvent.click(screen.getByRole('button', { name: 'Camminatore +1' }));
    expect(onBump).toHaveBeenCalledWith('walker', 1);
  });
});
