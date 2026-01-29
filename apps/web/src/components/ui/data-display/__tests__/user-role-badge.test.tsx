/**
 * UserRoleBadge Component Tests (Issue #2887)
 *
 * Tests rendering, styling, and accessibility for user role badges.
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { UserRoleBadge, type UserRole } from '../user-role-badge';

describe('UserRoleBadge', () => {
  describe('Rendering', () => {
    it('should render Admin badge with red styling', () => {
      render(<UserRoleBadge role="Admin" />);

      const badge = screen.getByText('Admin');
      expect(badge).toBeInTheDocument();
      expect(badge).toHaveClass('bg-red-100', 'text-red-800');
    });

    it('should render Editor badge with purple styling', () => {
      render(<UserRoleBadge role="Editor" />);

      const badge = screen.getByText('Editor');
      expect(badge).toBeInTheDocument();
      expect(badge).toHaveClass('bg-purple-100', 'text-purple-800');
    });

    it('should render User badge with blue styling', () => {
      render(<UserRoleBadge role="User" />);

      const badge = screen.getByText('User');
      expect(badge).toBeInTheDocument();
      expect(badge).toHaveClass('bg-blue-100', 'text-blue-800');
    });
  });

  describe('Icon Display', () => {
    it('should show icon by default', () => {
      render(<UserRoleBadge role="Admin" />);

      // Icon should be present (SVG element)
      const badge = screen.getByText('Admin');
      expect(badge.querySelector('svg')).toBeInTheDocument();
    });

    it('should hide icon when showIcon is false', () => {
      render(<UserRoleBadge role="Admin" showIcon={false} />);

      const badge = screen.getByText('Admin');
      expect(badge.querySelector('svg')).not.toBeInTheDocument();
    });
  });

  describe('Custom Styling', () => {
    it('should apply custom className', () => {
      render(<UserRoleBadge role="Admin" className="custom-class" />);

      const badge = screen.getByText('Admin');
      expect(badge).toHaveClass('custom-class');
    });

    it('should preserve role-specific classes with custom className', () => {
      render(<UserRoleBadge role="Admin" className="custom-class" />);

      const badge = screen.getByText('Admin');
      expect(badge).toHaveClass('bg-red-100', 'custom-class');
    });
  });

  describe('All Role Types', () => {
    const roles: UserRole[] = ['Admin', 'Editor', 'User'];

    it.each(roles)('should render %s role correctly', role => {
      render(<UserRoleBadge role={role} />);

      expect(screen.getByText(role)).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should allow additional ARIA attributes', () => {
      render(<UserRoleBadge role="Admin" aria-label="User role: Admin" />);

      const badge = screen.getByLabelText('User role: Admin');
      expect(badge).toBeInTheDocument();
    });
  });
});
