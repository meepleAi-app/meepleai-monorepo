import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { LibraryViewToggle, useLibraryView } from './LibraryViewToggle';

describe('useLibraryView', () => {
  beforeEach(() => localStorage.clear());

  it('defaults to grid', () => {
    function Probe() {
      const { view } = useLibraryView();
      return <span>{view}</span>;
    }
    render(<Probe />);
    expect(screen.getByText('grid')).toBeInTheDocument();
  });

  it('persists change to localStorage mai-library-view', () => {
    function Probe() {
      const { view, setView } = useLibraryView();
      return (
        <button type="button" onClick={() => setView('list')}>
          {view}
        </button>
      );
    }
    render(<Probe />);
    fireEvent.click(screen.getByRole('button'));
    expect(localStorage.getItem('mai-library-view')).toBe('list');
  });
});

describe('LibraryViewToggle', () => {
  beforeEach(() => localStorage.clear());

  it('renders grid and list buttons', () => {
    render(<LibraryViewToggle />);
    expect(screen.getByRole('button', { name: /griglia/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /lista/i })).toBeInTheDocument();
  });
});
