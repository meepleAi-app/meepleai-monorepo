import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { RoleCard } from '../role-card';

describe('RoleCard', () => {
  it('renders role information correctly', () => {
    render(
      <RoleCard
        role="Admin"
        userCount="5 users"
        description="Full system access"
        iconName="shield"
        colorClass="amber"
      />
    );

    expect(screen.getByText('Admin')).toBeInTheDocument();
    expect(screen.getByText('5 users')).toBeInTheDocument();
    expect(screen.getByText('Full system access')).toBeInTheDocument();
  });

  it('renders all role types', () => {
    const roles = [
      { role: 'Admin' as const, iconName: 'shield' as const, colorClass: 'amber' as const },
      { role: 'Editor' as const, iconName: 'pencil' as const, colorClass: 'blue' as const },
      { role: 'User' as const, iconName: 'user' as const, colorClass: 'green' as const },
      { role: 'Anonymous' as const, iconName: 'help-circle' as const, colorClass: 'gray' as const },
    ];

    roles.forEach(({ role, iconName, colorClass }) => {
      const { unmount } = render(
        <RoleCard
          role={role}
          userCount="10"
          description="Test"
          iconName={iconName}
          colorClass={colorClass}
        />
      );
      expect(screen.getByText(role)).toBeInTheDocument();
      unmount();
    });
  });

  it('applies correct color classes', () => {
    const { container } = render(
      <RoleCard
        role="Admin"
        userCount="5"
        description="Test"
        iconName="shield"
        colorClass="amber"
      />
    );

    const card = container.firstChild as HTMLElement;
    expect(card).toHaveClass('bg-white/70');
    expect(card).toHaveClass('backdrop-blur-md');
  });
});
