import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';

import { MechanicFilter } from '../MechanicFilter';

const MANY_MECHANICS = [
  'engine-building',
  'area-control',
  'deck-building',
  'worker-placement',
  'cooperative',
  'competitive',
  'dice-rolling',
  'puzzle-abstract',
];
const FEW_MECHANICS = ['engine-building', 'area-control', 'deck-building'];

describe('MechanicFilter', () => {
  it('mostra al massimo 5 chip quando ci sono più di 5 meccaniche', () => {
    render(<MechanicFilter mechanics={MANY_MECHANICS} selected={[]} onSelect={vi.fn()} />);
    // Con 8 meccaniche: 5 chip + 1 toggle button = 6
    const chipButtons = screen.getAllByRole('button');
    expect(chipButtons).toHaveLength(6);
    expect(screen.getByTestId('mechanic-toggle')).toBeInTheDocument();
  });

  it('non mostra il toggle se ci sono 5 o meno meccaniche', () => {
    render(<MechanicFilter mechanics={FEW_MECHANICS} selected={[]} onSelect={vi.fn()} />);
    expect(screen.getAllByRole('button')).toHaveLength(3);
    expect(screen.queryByTestId('mechanic-toggle')).not.toBeInTheDocument();
  });

  it('espande tutti i chip al click sul toggle', async () => {
    const user = userEvent.setup();
    render(<MechanicFilter mechanics={MANY_MECHANICS} selected={[]} onSelect={vi.fn()} />);
    const toggle = screen.getByTestId('mechanic-toggle');
    await user.click(toggle);
    // Tutti 8 chip + toggle button = 9
    expect(screen.getAllByRole('button')).toHaveLength(9);
  });

  it('collassa di nuovo al secondo click sul toggle', async () => {
    const user = userEvent.setup();
    render(<MechanicFilter mechanics={MANY_MECHANICS} selected={[]} onSelect={vi.fn()} />);
    const toggle = screen.getByTestId('mechanic-toggle');
    await user.click(toggle); // espandi
    await user.click(toggle); // collassa
    expect(screen.getAllByRole('button')).toHaveLength(6);
  });

  it('chiama onSelect con il mechanic slug al click', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<MechanicFilter mechanics={FEW_MECHANICS} selected={[]} onSelect={onSelect} />);
    await user.click(screen.getByText('Engine Building'));
    expect(onSelect).toHaveBeenCalledWith('engine-building');
  });

  it('mostra chip selezionati con aria-pressed=true', () => {
    render(
      <MechanicFilter mechanics={FEW_MECHANICS} selected={['engine-building']} onSelect={vi.fn()} />
    );
    expect(screen.getByText('Engine Building').closest('button')).toHaveAttribute(
      'aria-pressed',
      'true'
    );
  });

  it('il testo del toggle indica quanti chip sono nascosti', () => {
    render(<MechanicFilter mechanics={MANY_MECHANICS} selected={[]} onSelect={vi.fn()} />);
    // 8 - 5 = 3 nascosti
    expect(screen.getByTestId('mechanic-toggle')).toHaveTextContent('+3 altri');
  });
});
