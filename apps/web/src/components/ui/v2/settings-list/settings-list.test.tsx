import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { SettingsList } from './settings-list';

describe('SettingsList', () => {
  it('renders children inside a list with role="list"', () => {
    render(
      <SettingsList>
        <li>Item A</li>
        <li>Item B</li>
      </SettingsList>
    );
    const list = screen.getByRole('list');
    expect(list.tagName).toBe('UL');
    expect(list.children).toHaveLength(2);
    expect(screen.getByText('Item A')).toBeInTheDocument();
    expect(screen.getByText('Item B')).toBeInTheDocument();
  });

  it('merges className on the nav wrapper', () => {
    const { container } = render(
      <SettingsList className="custom-class">
        <li>Only</li>
      </SettingsList>
    );
    const nav = container.querySelector('nav');
    expect(nav).toHaveClass('custom-class');
  });

  it('uses default aria-label when none provided', () => {
    render(
      <SettingsList>
        <li>X</li>
      </SettingsList>
    );
    expect(screen.getByRole('navigation', { name: 'Impostazioni' })).toBeInTheDocument();
  });

  it('accepts custom aria-label', () => {
    render(
      <SettingsList ariaLabel="Account settings">
        <li>X</li>
      </SettingsList>
    );
    expect(screen.getByRole('navigation', { name: 'Account settings' })).toBeInTheDocument();
  });
});
