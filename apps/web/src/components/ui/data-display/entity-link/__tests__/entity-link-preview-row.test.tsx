/**
 * EntityLinkPreviewRow — C2
 * Tests for Issue #5158 / #5165
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { EntityLinkPreviewRow } from '../entity-link-preview-row';

describe('EntityLinkPreviewRow', () => {
  const defaultProps = {
    linkType: 'ExpansionOf' as const,
    targetName: 'Wingspan',
    totalCount: 1,
  };

  it('renders the target name', () => {
    render(<EntityLinkPreviewRow {...defaultProps} />);
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('returns null when totalCount is 0', () => {
    const { container } = render(<EntityLinkPreviewRow {...defaultProps} totalCount={0} />);
    expect(container.firstChild).toBeNull();
  });

  it('returns null when totalCount is negative', () => {
    const { container } = render(<EntityLinkPreviewRow {...defaultProps} totalCount={-1} />);
    expect(container.firstChild).toBeNull();
  });

  it('shows overflow count when totalCount > 1', () => {
    render(<EntityLinkPreviewRow {...defaultProps} totalCount={4} />);
    expect(screen.getByText('+3')).toBeInTheDocument();
  });

  it('does not show overflow when totalCount = 1', () => {
    render(<EntityLinkPreviewRow {...defaultProps} totalCount={1} />);
    expect(screen.queryByText(/^\+/)).toBeNull();
  });

  it('calls onClick when clicked', () => {
    const handleClick = vi.fn();
    render(<EntityLinkPreviewRow {...defaultProps} onClick={handleClick} />);
    fireEvent.click(screen.getByTestId('entity-link-preview-row'));
    expect(handleClick).toHaveBeenCalledOnce();
  });

  it('stops propagation on click', () => {
    const parentHandler = vi.fn();
    const clickHandler = vi.fn();
    render(
      <div onClick={parentHandler}>
        <EntityLinkPreviewRow {...defaultProps} onClick={clickHandler} />
      </div>
    );
    fireEvent.click(screen.getByTestId('entity-link-preview-row'));
    expect(parentHandler).not.toHaveBeenCalled();
    expect(clickHandler).toHaveBeenCalled();
  });

  it('renders the dashed divider before the row', () => {
    const { container } = render(<EntityLinkPreviewRow {...defaultProps} />);
    const divider = container.querySelector('.border-dashed');
    expect(divider).toBeInTheDocument();
  });

  it('has accessible aria-label with count', () => {
    render(<EntityLinkPreviewRow {...defaultProps} totalCount={3} />);
    expect(screen.getByRole('button', { name: /3 links — view all/i })).toBeInTheDocument();
  });

  it('uses singular form for count=1', () => {
    render(<EntityLinkPreviewRow {...defaultProps} totalCount={1} />);
    expect(screen.getByRole('button', { name: /1 link — view all/i })).toBeInTheDocument();
  });

  it('accepts custom data-testid', () => {
    render(<EntityLinkPreviewRow {...defaultProps} data-testid="custom-row" />);
    expect(screen.getByTestId('custom-row')).toBeInTheDocument();
  });
});
