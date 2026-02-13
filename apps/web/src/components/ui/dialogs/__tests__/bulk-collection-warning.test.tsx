/**
 * Tests for BulkCollectionWarning component
 * Issue #4268 - Phase 3: Bulk Collection Actions
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { BulkCollectionWarning } from '../bulk-collection-warning';

import type { BulkAssociatedDataDto } from '@/lib/api/schemas/collections.schemas';

describe('BulkCollectionWarning', () => {
  const mockAggregatedData: BulkAssociatedDataDto = {
    totalCustomAgents: 5,
    totalPrivatePdfs: 3,
    totalChatSessions: 18,
    totalGameSessions: 42,
    totalChecklistItems: 15,
    totalLabels: 8,
  };

  const emptyAggregatedData: BulkAssociatedDataDto = {
    totalCustomAgents: 0,
    totalPrivatePdfs: 0,
    totalChatSessions: 0,
    totalGameSessions: 0,
    totalChecklistItems: 0,
    totalLabels: 0,
  };

  it('renders with aggregated data items', () => {
    render(
      <BulkCollectionWarning
        entityCount={12}
        entityType="giochi"
        aggregatedData={mockAggregatedData}
        open={true}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    // Title shows entity count
    expect(screen.getByText(/Rimuovi 12 giochi dalla Collezione/)).toBeInTheDocument();

    // All data loss items are rendered
    expect(screen.getByText('5 agenti AI personalizzati')).toBeInTheDocument();
    expect(screen.getByText("18 chat con l'agente")).toBeInTheDocument();
    expect(screen.getByText('3 PDF privati caricati')).toBeInTheDocument();
    expect(screen.getByText('42 sessioni registrate')).toBeInTheDocument();
    expect(screen.getByText('15 task nella checklist')).toBeInTheDocument();
    expect(screen.getByText('8 etichette personalizzate')).toBeInTheDocument();
  });

  it('renders empty state when no associated data', () => {
    render(
      <BulkCollectionWarning
        entityCount={5}
        entityType="giochi"
        aggregatedData={emptyAggregatedData}
        open={true}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    expect(
      screen.getByText('Non ci sono dati associati che verranno persi.')
    ).toBeInTheDocument();
  });

  it('calls onConfirm when confirm button clicked', async () => {
    const user = userEvent.setup();
    const mockConfirm = vi.fn();

    render(
      <BulkCollectionWarning
        entityCount={3}
        entityType="giochi"
        aggregatedData={emptyAggregatedData}
        open={true}
        onConfirm={mockConfirm}
        onCancel={vi.fn()}
      />
    );

    await user.click(screen.getByText('Rimuovi Definitivamente'));
    expect(mockConfirm).toHaveBeenCalledTimes(1);
  });

  it('calls onCancel when cancel button clicked', async () => {
    const user = userEvent.setup();
    const mockCancel = vi.fn();

    render(
      <BulkCollectionWarning
        entityCount={3}
        entityType="giochi"
        aggregatedData={emptyAggregatedData}
        open={true}
        onConfirm={vi.fn()}
        onCancel={mockCancel}
      />
    );

    await user.click(screen.getByText('Annulla'));
    expect(mockCancel).toHaveBeenCalledTimes(1);
  });

  it('disables buttons when loading', () => {
    render(
      <BulkCollectionWarning
        entityCount={3}
        entityType="giochi"
        aggregatedData={emptyAggregatedData}
        open={true}
        loading={true}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    expect(screen.getByText('Annulla')).toBeDisabled();
    expect(screen.getByText('Rimozione...')).toBeDisabled();
  });

  it('only renders items with counts > 0', () => {
    const partialData: BulkAssociatedDataDto = {
      totalEntities: 3,
      totalCustomAgents: 2,
      totalPrivatePdfs: 0, // Should not render
      totalChatSessions: 5,
      totalGameSessions: 0, // Should not render
      totalChecklistItems: 1,
      totalLabels: 0, // Should not render
    };

    render(
      <BulkCollectionWarning
        entityCount={3}
        entityType="giochi"
        aggregatedData={partialData}
        open={true}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    // Should render
    expect(screen.getByText('2 agenti AI personalizzati')).toBeInTheDocument();
    expect(screen.getByText("5 chat con l'agente")).toBeInTheDocument();
    expect(screen.getByText('1 task nella checklist')).toBeInTheDocument();

    // Should NOT render
    expect(screen.queryByText(/PDF privati/)).not.toBeInTheDocument();
    expect(screen.queryByText(/sessioni registrate/)).not.toBeInTheDocument();
    expect(screen.queryByText(/etichette personalizzate/)).not.toBeInTheDocument();
  });

  it('uses default entityType when not provided', () => {
    render(
      <BulkCollectionWarning
        entityCount={5}
        aggregatedData={emptyAggregatedData}
        open={true}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    expect(screen.getByText(/Rimuovi 5 elementi dalla Collezione/)).toBeInTheDocument();
  });
});
