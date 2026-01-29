/**
 * UserStatusIndicator Component Tests (Issue #2887)
 *
 * Tests rendering, status derivation, and styling for user status indicators.
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { UserStatusIndicator, getUserStatus, type UserStatus } from '../user-status-indicator';

describe('UserStatusIndicator', () => {
  describe('Rendering', () => {
    it('should render Active status with green styling', () => {
      render(<UserStatusIndicator status="Active" />);

      const text = screen.getByText('Active');
      expect(text).toBeInTheDocument();
      // Color class is on parent element
      expect(text.parentElement).toHaveClass('text-green-700');
    });

    it('should render Inactive status with gray styling', () => {
      render(<UserStatusIndicator status="Inactive" />);

      const text = screen.getByText('Inactive');
      expect(text).toBeInTheDocument();
      // Color class is on parent element
      expect(text.parentElement).toHaveClass('text-gray-500');
    });

    it('should render Suspended status with red styling and pulsing animation', () => {
      render(<UserStatusIndicator status="Suspended" />);

      const container = screen.getByText('Suspended').parentElement;
      expect(container).toBeInTheDocument();

      // Check for pulsing dot
      const dot = container?.querySelector('[aria-hidden="true"]');
      expect(dot).toHaveClass('animate-pulse', 'bg-red-500');
    });
  });

  describe('Label Display', () => {
    it('should show label by default', () => {
      render(<UserStatusIndicator status="Active" />);

      expect(screen.getByText('Active')).toBeInTheDocument();
    });

    it('should hide label when showLabel is false', () => {
      render(<UserStatusIndicator status="Active" showLabel={false} />);

      expect(screen.queryByText('Active')).not.toBeInTheDocument();
      // Dot should still be present
      const container = document.querySelector('[aria-hidden="true"]');
      expect(container).toBeInTheDocument();
    });
  });

  describe('Status Dot Colors', () => {
    it('should have green dot for Active status', () => {
      render(<UserStatusIndicator status="Active" />);

      const container = screen.getByText('Active').parentElement;
      const dot = container?.querySelector('[aria-hidden="true"]');
      expect(dot).toHaveClass('bg-green-500');
    });

    it('should have gray dot for Inactive status', () => {
      render(<UserStatusIndicator status="Inactive" />);

      const container = screen.getByText('Inactive').parentElement;
      const dot = container?.querySelector('[aria-hidden="true"]');
      expect(dot).toHaveClass('bg-gray-400');
    });

    it('should have red pulsing dot for Suspended status', () => {
      render(<UserStatusIndicator status="Suspended" />);

      const container = screen.getByText('Suspended').parentElement;
      const dot = container?.querySelector('[aria-hidden="true"]');
      expect(dot).toHaveClass('bg-red-500', 'animate-pulse');
    });
  });

  describe('Custom Styling', () => {
    it('should apply custom className', () => {
      render(<UserStatusIndicator status="Active" className="custom-class" />);

      const indicator = screen.getByText('Active').parentElement;
      expect(indicator).toHaveClass('custom-class');
    });
  });

  describe('All Status Types', () => {
    const statuses: UserStatus[] = ['Active', 'Inactive', 'Suspended'];

    it.each(statuses)('should render %s status correctly', status => {
      render(<UserStatusIndicator status={status} />);

      expect(screen.getByText(status)).toBeInTheDocument();
    });
  });
});

describe('getUserStatus', () => {
  let dateNowSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    // Mock current date to 2024-01-15
    dateNowSpy = vi.spyOn(Date, 'now').mockImplementation(() => new Date('2024-01-15').getTime());
  });

  afterEach(() => {
    dateNowSpy.mockRestore();
  });

  it('should return Suspended for suspended users', () => {
    const user = {
      isSuspended: true,
      lastSeenAt: '2024-01-14T12:00:00Z',
    };

    expect(getUserStatus(user)).toBe('Suspended');
  });

  it('should return Active for recently active users', () => {
    const user = {
      isSuspended: false,
      lastSeenAt: '2024-01-10T12:00:00Z', // 5 days ago
    };

    expect(getUserStatus(user)).toBe('Active');
  });

  it('should return Inactive for users not seen in last 30 days', () => {
    const user = {
      isSuspended: false,
      lastSeenAt: '2023-12-01T12:00:00Z', // More than 30 days ago
    };

    expect(getUserStatus(user)).toBe('Inactive');
  });

  it('should return Active for users with no lastSeenAt', () => {
    const user = {
      isSuspended: false,
      lastSeenAt: null,
    };

    expect(getUserStatus(user)).toBe('Active');
  });

  it('should prioritize Suspended over Inactive', () => {
    const user = {
      isSuspended: true,
      lastSeenAt: '2023-06-01T12:00:00Z', // Very old, but suspended
    };

    expect(getUserStatus(user)).toBe('Suspended');
  });

  it('should handle users seen exactly 30 days ago as Active', () => {
    // 30 days before Jan 15 is Dec 16
    const user = {
      isSuspended: false,
      lastSeenAt: '2023-12-16T12:00:00Z',
    };

    expect(getUserStatus(user)).toBe('Active');
  });

  it('should handle users seen 31 days ago as Inactive', () => {
    // 31 days before Jan 15 is Dec 15
    const user = {
      isSuspended: false,
      lastSeenAt: '2023-12-14T12:00:00Z',
    };

    expect(getUserStatus(user)).toBe('Inactive');
  });
});
