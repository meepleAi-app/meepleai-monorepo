/**
 * EntityLinkCard — C4
 * Tests for Issue #5160 / #5165
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { EntityLinkCard } from '../entity-link-card';
import type { EntityLinkDto } from '../entity-link-types';

const MOCK_LINK: EntityLinkDto = {
  id: 'link-123',
  sourceEntityType: 'Game',
  sourceEntityId: 'game-1',
  targetEntityType: 'Game',
  targetEntityId: 'game-2',
  linkType: 'ExpansionOf',
  isOwner: true,
  isBggImported: false,
  isAdminApproved: false,
  createdAt: '2026-01-01T00:00:00Z',
};

describe('EntityLinkCard', () => {
  it('renders the target name', () => {
    render(<EntityLinkCard link={MOCK_LINK} targetName="Wingspan: European Expansion" />);
    expect(screen.getByText('Wingspan: European Expansion')).toBeInTheDocument();
  });

  it('renders the link type chip', () => {
    render(<EntityLinkCard link={MOCK_LINK} targetName="Wingspan" />);
    // chip label contains 'expansion of'
    expect(screen.getByText(/expansion of/i)).toBeInTheDocument();
  });

  it('shows navigate button when onNavigate is provided', () => {
    const onNav = vi.fn();
    render(<EntityLinkCard link={MOCK_LINK} targetName="Wingspan" onNavigate={onNav} />);
    expect(screen.getByRole('button', { name: /navigate to wingspan/i })).toBeInTheDocument();
  });

  it('calls onNavigate with correct args when navigate clicked', () => {
    const onNav = vi.fn();
    render(<EntityLinkCard link={MOCK_LINK} targetName="Wingspan" onNavigate={onNav} />);
    fireEvent.click(screen.getByRole('button', { name: /navigate to wingspan/i }));
    expect(onNav).toHaveBeenCalledWith('Game', 'game-2');
  });

  it('hides navigate button when onNavigate is not provided', () => {
    render(<EntityLinkCard link={MOCK_LINK} targetName="Wingspan" />);
    expect(screen.queryByRole('button', { name: /navigate/i })).toBeNull();
  });

  it('shows remove button when isOwner and onRemove provided', () => {
    const onRemove = vi.fn();
    render(<EntityLinkCard link={MOCK_LINK} targetName="Wingspan" onRemove={onRemove} />);
    expect(screen.getByRole('button', { name: /remove link to wingspan/i })).toBeInTheDocument();
  });

  it('calls onRemove with linkId when remove clicked', () => {
    const onRemove = vi.fn();
    render(<EntityLinkCard link={MOCK_LINK} targetName="Wingspan" onRemove={onRemove} />);
    fireEvent.click(screen.getByRole('button', { name: /remove link to wingspan/i }));
    expect(onRemove).toHaveBeenCalledWith('link-123');
  });

  it('hides remove button when showBggBadge is true', () => {
    const onRemove = vi.fn();
    render(
      <EntityLinkCard link={MOCK_LINK} targetName="Wingspan" onRemove={onRemove} showBggBadge />
    );
    expect(screen.queryByRole('button', { name: /remove/i })).toBeNull();
  });

  it('shows BGG badge when showBggBadge is true', () => {
    render(<EntityLinkCard link={MOCK_LINK} targetName="Wingspan" showBggBadge />);
    expect(screen.getByText('BGG')).toBeInTheDocument();
  });

  it('has data-testid="entity-link-card" by default', () => {
    render(<EntityLinkCard link={MOCK_LINK} targetName="Wingspan" />);
    expect(screen.getByTestId('entity-link-card')).toBeInTheDocument();
  });

  it('stops propagation on remove click', () => {
    const parentHandler = vi.fn();
    const onRemove = vi.fn();
    render(
      <div onClick={parentHandler}>
        <EntityLinkCard link={MOCK_LINK} targetName="Wingspan" onRemove={onRemove} />
      </div>
    );
    fireEvent.click(screen.getByRole('button', { name: /remove/i }));
    expect(parentHandler).not.toHaveBeenCalled();
  });
});
