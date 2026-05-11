import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ThemeToggle } from './theme-toggle';

describe('ThemeToggle', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('dark');
  });

  it('toggles dark class on html element', () => {
    render(<ThemeToggle />);
    const btn = screen.getByRole('button', { name: /tema/i });
    fireEvent.click(btn);
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    fireEvent.click(btn);
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('persists choice in localStorage mai-theme', () => {
    render(<ThemeToggle />);
    fireEvent.click(screen.getByRole('button', { name: /tema/i }));
    expect(localStorage.getItem('mai-theme')).toBe('dark');
  });

  it('reads initial value from localStorage', () => {
    localStorage.setItem('mai-theme', 'dark');
    render(<ThemeToggle />);
    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });
});
