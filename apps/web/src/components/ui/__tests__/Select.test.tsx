import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';

describe('Select', () => {
  it('renders trigger with placeholder and opens content', async () => {
    const user = userEvent.setup();
    render(
      <Select>
        <SelectTrigger aria-label="game select">
          <SelectValue placeholder="Choose a game" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="catan">Catan</SelectItem>
          <SelectItem value="carcassonne">Carcassonne</SelectItem>
        </SelectContent>
      </Select>
    );

    const trigger = screen.getByRole('combobox', { name: /game select/i });
    expect(trigger).toBeInTheDocument();
    await user.click(trigger);
    expect(await screen.findByRole('option', { name: 'Catan' })).toBeInTheDocument();
  });

  it('calls onValueChange when an item is selected', async () => {
    const user = userEvent.setup();
    const onValueChange = vi.fn();
    render(
      <Select onValueChange={onValueChange}>
        <SelectTrigger aria-label="game select">
          <SelectValue placeholder="Choose" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="catan">Catan</SelectItem>
        </SelectContent>
      </Select>
    );

    const trigger = screen.getByRole('combobox', { name: /game select/i });
    await user.click(trigger);
    const option = await screen.findByRole('option', { name: 'Catan' });
    await user.click(option);
    expect(onValueChange).toHaveBeenCalledWith('catan');
  });
});
