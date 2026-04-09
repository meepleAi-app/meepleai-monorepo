import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useMyHandStore } from '@/stores/my-hand/store';
import { MyHandSidebar } from '../MyHandSidebar';

vi.mock('../MyHandSlotPicker', () => ({
  MyHandSlotPicker: ({ isOpen }: { isOpen: boolean }) =>
    isOpen ? <div data-testid="slot-picker">Picker</div> : null,
}));

describe('MyHandSidebar', () => {
  beforeEach(() => {
    useMyHandStore.setState(useMyHandStore.getInitialState());
  });

  it('renders 4 slots', () => {
    render(<MyHandSidebar />);
    expect(screen.getAllByRole('button', { name: /seleziona/i })).toHaveLength(4);
  });

  it('toggles collapsed state', () => {
    render(<MyHandSidebar />);
    const toggleBtn = screen.getByRole('button', { name: /comprimi|espandi/i });
    fireEvent.click(toggleBtn);
    expect(useMyHandStore.getState().isSidebarCollapsed).toBe(true);
  });

  it('opens picker when slot assign is clicked', () => {
    render(<MyHandSidebar />);
    fireEvent.click(screen.getAllByRole('button', { name: /seleziona/i })[0]);
    expect(screen.getByTestId('slot-picker')).toBeInTheDocument();
  });
});
