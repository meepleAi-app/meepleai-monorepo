/**
 * EntityLinkBadge — C1
 * Tests for Issue #5157 / #5165
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { EntityLinkBadge } from '../entity-link-badge';

describe('EntityLinkBadge', () => {
  it('renders count when positive', () => {
    render(<EntityLinkBadge count={3} />);
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('returns null when count is 0', () => {
    const { container } = render(<EntityLinkBadge count={0} />);
    expect(container.firstChild).toBeNull();
  });

  it('returns null when count is negative', () => {
    const { container } = render(<EntityLinkBadge count={-1} />);
    expect(container.firstChild).toBeNull();
  });

  it('calls onClick when clicked', () => {
    const handleClick = vi.fn();
    render(<EntityLinkBadge count={2} onClick={handleClick} />);
    fireEvent.click(screen.getByTestId('entity-link-badge'));
    expect(handleClick).toHaveBeenCalledOnce();
  });

  it('stops event propagation on click', () => {
    const parentHandler = vi.fn();
    render(
      <div onClick={parentHandler}>
        <EntityLinkBadge count={1} />
      </div>
    );
    fireEvent.click(screen.getByTestId('entity-link-badge'));
    expect(parentHandler).not.toHaveBeenCalled();
  });

  it('has accessible aria-label', () => {
    render(<EntityLinkBadge count={5} />);
    expect(screen.getByRole('button', { name: /5 connections/i })).toBeInTheDocument();
  });

  it('uses singular form for count=1', () => {
    render(<EntityLinkBadge count={1} />);
    expect(screen.getByRole('button', { name: /1 connection —/i })).toBeInTheDocument();
  });

  it('applies custom className', () => {
    render(<EntityLinkBadge count={1} className="my-class" />);
    expect(screen.getByTestId('entity-link-badge')).toHaveClass('my-class');
  });

  it('accepts custom data-testid', () => {
    render(<EntityLinkBadge count={1} data-testid="custom-badge" />);
    expect(screen.getByTestId('custom-badge')).toBeInTheDocument();
  });
});
