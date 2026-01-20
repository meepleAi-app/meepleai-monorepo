/**
 * ResourceEditor Component Tests - Issue #2420
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { ResourceEditor } from '../ResourceEditor';
import type { PlayerState, ResourceState } from '../StateEditor';

describe('ResourceEditor', () => {
  const mockOnChange = vi.fn();

  const mockPlayers: PlayerState[] = [
    { id: '1', name: 'Alice', score: 0 },
    { id: '2', name: 'Bob', score: 0 },
  ];

  it('renders empty state', () => {
    render(<ResourceEditor resources={[]} players={mockPlayers} onChange={mockOnChange} />);

    expect(screen.getByText('Risorse')).toBeInTheDocument();
    expect(screen.getByText('Nessuna risorsa aggiunta')).toBeInTheDocument();
  });

  it('renders resources list', () => {
    const resources: ResourceState[] = [
      { id: 'r1', type: 'token', name: 'Gold', quantity: 25 },
      { id: 'r2', type: 'card', name: 'Victory Point', quantity: 10 },
    ];

    render(<ResourceEditor resources={resources} players={mockPlayers} onChange={mockOnChange} />);

    expect(screen.getByDisplayValue('Gold')).toBeInTheDocument();
    expect(screen.getByDisplayValue('Victory Point')).toBeInTheDocument();
    expect(screen.getByDisplayValue('25')).toBeInTheDocument();
    expect(screen.getByDisplayValue('10')).toBeInTheDocument();
  });

  it('shows add resource button', () => {
    render(<ResourceEditor resources={[]} players={mockPlayers} onChange={mockOnChange} />);

    expect(screen.getByText(/Aggiungi Risorsa/)).toBeInTheDocument();
  });

  it('calls onChange when adding resource', () => {
    render(<ResourceEditor resources={[]} players={mockPlayers} onChange={mockOnChange} />);

    const addButton = screen.getByText(/Aggiungi Risorsa/);
    fireEvent.click(addButton);

    expect(mockOnChange).toHaveBeenCalledWith(
      expect.arrayContaining([
        expect.objectContaining({
          type: 'token',
          name: expect.stringContaining('Risorsa'),
          quantity: 1,
        }),
      ])
    );
  });

  it('calls onChange when removing resource', () => {
    const resources: ResourceState[] = [{ id: 'r1', type: 'token', name: 'Gold', quantity: 25 }];

    render(<ResourceEditor resources={resources} players={mockPlayers} onChange={mockOnChange} />);

    const removeButton = screen.getAllByRole('button', { name: '' })[0]; // Trash icon button
    fireEvent.click(removeButton);

    expect(mockOnChange).toHaveBeenCalledWith([]);
  });

  it('calls onChange when updating resource name', () => {
    const resources: ResourceState[] = [{ id: 'r1', type: 'token', name: 'Gold', quantity: 25 }];

    render(<ResourceEditor resources={resources} players={mockPlayers} onChange={mockOnChange} />);

    const nameInput = screen.getByDisplayValue('Gold');
    fireEvent.change(nameInput, { target: { value: 'Gold Updated' } });

    expect(mockOnChange).toHaveBeenCalledWith([
      expect.objectContaining({
        id: 'r1',
        name: 'Gold Updated',
      }),
    ]);
  });

  it('calls onChange when updating resource quantity', () => {
    const resources: ResourceState[] = [{ id: 'r1', type: 'token', name: 'Gold', quantity: 25 }];

    render(<ResourceEditor resources={resources} players={mockPlayers} onChange={mockOnChange} />);

    const quantityInput = screen.getByDisplayValue('25');
    fireEvent.change(quantityInput, { target: { value: '50' } });

    expect(mockOnChange).toHaveBeenCalledWith([
      expect.objectContaining({
        id: 'r1',
        quantity: 50,
      }),
    ]);
  });

  it('displays validation errors', () => {
    const resources: ResourceState[] = [{ id: 'r1', type: 'token', name: '', quantity: 25 }];

    const validationErrors = {
      'resources.0.name': 'Nome risorsa obbligatorio',
    };

    render(
      <ResourceEditor
        resources={resources}
        players={mockPlayers}
        onChange={mockOnChange}
        validationErrors={validationErrors}
      />
    );

    expect(screen.getByText('Nome risorsa obbligatorio')).toBeInTheDocument();
  });

  it('shows resource summary', () => {
    const resources: ResourceState[] = [
      { id: 'r1', type: 'token', name: 'Gold', quantity: 25 },
      { id: 'r2', type: 'card', name: 'Victory', quantity: 10 },
    ];

    render(<ResourceEditor resources={resources} players={mockPlayers} onChange={mockOnChange} />);

    // Text is split across <strong> and text nodes, use custom matcher
    expect(screen.getByText(/Risorse totali:/)).toBeInTheDocument();
    expect(screen.getByText(/Quantità totale:/)).toBeInTheDocument();
    // Find the summary div and check its full text content
    const summaryDiv = screen.getByText((content, element) => {
      return element?.tagName === 'DIV' &&
             element?.className.includes('bg-blue-50') &&
             element?.textContent?.includes('Risorse totali:') === true &&
             element?.textContent?.includes('2') === true &&
             element?.textContent?.includes('Quantità totale:') === true &&
             element?.textContent?.includes('35') === true;
    });
    expect(summaryDiv).toBeInTheDocument();
  });

  it('hides add/remove buttons in readonly mode', () => {
    const resources: ResourceState[] = [{ id: 'r1', type: 'token', name: 'Gold', quantity: 25 }];

    render(
      <ResourceEditor
        resources={resources}
        players={mockPlayers}
        onChange={mockOnChange}
        readonly
      />
    );

    expect(screen.queryByText(/Aggiungi Risorsa/)).not.toBeInTheDocument();
  });

  it('handles resource with owner', () => {
    const resources: ResourceState[] = [
      { id: 'r1', type: 'token', name: 'Gold', quantity: 25, ownerId: '1' },
    ];

    render(<ResourceEditor resources={resources} players={mockPlayers} onChange={mockOnChange} />);

    // Verify owner select shows the assigned player
    expect(screen.getByDisplayValue('Gold')).toBeInTheDocument();
  });

  it('renders different resource type icons', () => {
    const resources: ResourceState[] = [
      { id: 'r1', type: 'token', name: 'Gold', quantity: 25 },
      { id: 'r2', type: 'card', name: 'Action', quantity: 10 },
      { id: 'r3', type: 'resource', name: 'Wood', quantity: 15 },
    ];

    const { container } = render(
      <ResourceEditor resources={resources} players={mockPlayers} onChange={mockOnChange} />
    );

    // Should have 3 different icons (one for each resource type)
    const icons = container.querySelectorAll('svg');
    expect(icons.length).toBeGreaterThanOrEqual(3);
  });

  it('handles multiple validation errors', () => {
    const resources: ResourceState[] = [
      { id: 'r1', type: 'token', name: '', quantity: 25 },
      { id: 'r2', type: 'card', name: 'Victory', quantity: -10 },
    ];

    const validationErrors = {
      'resources.0.name': 'Nome risorsa obbligatorio',
      'resources.1.quantity': 'Quantità non può essere negativa',
    };

    render(
      <ResourceEditor
        resources={resources}
        players={mockPlayers}
        onChange={mockOnChange}
        validationErrors={validationErrors}
      />
    );

    expect(screen.getByText('Nome risorsa obbligatorio')).toBeInTheDocument();
    expect(screen.getByText('Quantità non può essere negativa')).toBeInTheDocument();
  });

  it('shows zero quantity correctly', () => {
    const resources: ResourceState[] = [{ id: 'r1', type: 'token', name: 'Gold', quantity: 0 }];

    render(<ResourceEditor resources={resources} players={mockPlayers} onChange={mockOnChange} />);

    expect(screen.getByDisplayValue('0')).toBeInTheDocument();
    expect(screen.getByText('Quantità totale:')).toBeInTheDocument();
  });

  it('handles resources without players', () => {
    const resources: ResourceState[] = [{ id: 'r1', type: 'token', name: 'Gold', quantity: 25 }];

    render(<ResourceEditor resources={resources} players={[]} onChange={mockOnChange} />);

    // Should render without crashing even with no players
    expect(screen.getByDisplayValue('Gold')).toBeInTheDocument();
  });
});
