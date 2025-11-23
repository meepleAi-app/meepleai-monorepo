/**
 * SearchFilters Component Tests
 * Unit tests for search filter UI (Issue #1101)
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { SearchFilters } from '../search/SearchFilters';
import type { SearchFilters as SearchFiltersType, Game, Agent } from '@/types';
import { createMockAgent } from '@/__tests__/fixtures/common-fixtures';

// Mock data
const mockGames: Game[] = [
  { id: '770e8400-e29b-41d4-a716-000000000001', title: 'Chess', createdAt: '2024-01-01' },
  { id: '770e8400-e29b-41d4-a716-000000000002', title: 'Go', createdAt: '2024-01-02' },
  { id: '770e8400-e29b-41d4-a716-000000000003', title: 'Monopoly', createdAt: '2024-01-03' },
];

const mockAgents: Agent[] = [
  createMockAgent({ id: 'agent-1', gameId: 'game-1', name: 'Chess Master', type: 'qa' }),
  createMockAgent({ id: 'agent-2', gameId: 'game-2', name: 'Go Sensei', type: 'explain' }),
];

describe('SearchFilters', () => {
  const mockOnFiltersChange = jest.fn();

  beforeEach(() => {
    mockOnFiltersChange.mockClear();
  });

  it('should render all filter sections', () => {
    render(
      <SearchFilters
        filters={{}}
        onFiltersChange={mockOnFiltersChange}
        games={mockGames}
        agents={mockAgents}
      />
    );

    expect(screen.getByText('Filters')).toBeInTheDocument();
    expect(screen.getByLabelText('Game')).toBeInTheDocument();
    expect(screen.getByLabelText('Agent')).toBeInTheDocument();
    expect(screen.getByText('Date Range')).toBeInTheDocument();
    expect(screen.getByText('Result Types')).toBeInTheDocument();
  });

  it('should render Clear All button when filters are active', () => {
    const filters: SearchFiltersType = { gameId: '770e8400-e29b-41d4-a716-000000000001' };

    render(
      <SearchFilters
        filters={filters}
        onFiltersChange={mockOnFiltersChange}
        games={mockGames}
        agents={mockAgents}
      />
    );

    expect(screen.getByText('Clear All')).toBeInTheDocument();
  });

  it('should not render Clear All button when no filters are active', () => {
    render(
      <SearchFilters
        filters={{}}
        onFiltersChange={mockOnFiltersChange}
        games={mockGames}
        agents={mockAgents}
      />
    );

    expect(screen.queryByText('Clear All')).not.toBeInTheDocument();
  });

  it('should clear all filters when Clear All is clicked', () => {
    const filters: SearchFiltersType = {
      gameId: '770e8400-e29b-41d4-a716-000000000001',
      agentId: 'agent-1',
      types: ['message'],
    };

    render(
      <SearchFilters
        filters={filters}
        onFiltersChange={mockOnFiltersChange}
        games={mockGames}
        agents={mockAgents}
      />
    );

    const clearButton = screen.getByText('Clear All');
    fireEvent.click(clearButton);

    expect(mockOnFiltersChange).toHaveBeenCalledWith({});
  });

  it('should render result type buttons', () => {
    render(
      <SearchFilters
        filters={{}}
        onFiltersChange={mockOnFiltersChange}
        games={mockGames}
        agents={mockAgents}
      />
    );

    expect(screen.getByText('Messages')).toBeInTheDocument();
    expect(screen.getByText('Chats')).toBeInTheDocument();
    expect(screen.getByText('Games')).toBeInTheDocument();
    expect(screen.getByText('Agents')).toBeInTheDocument();
    expect(screen.getByText('PDFs')).toBeInTheDocument();
  });

  it('should toggle result type when clicked', () => {
    render(
      <SearchFilters
        filters={{}}
        onFiltersChange={mockOnFiltersChange}
        games={mockGames}
        agents={mockAgents}
      />
    );

    const messagesButton = screen.getByText('Messages');
    fireEvent.click(messagesButton);

    expect(mockOnFiltersChange).toHaveBeenCalledWith({
      types: ['message'],
    });
  });

  it('should remove type when toggled off', () => {
    const filters: SearchFiltersType = { types: ['message', 'chat'] };

    render(
      <SearchFilters
        filters={filters}
        onFiltersChange={mockOnFiltersChange}
        games={mockGames}
        agents={mockAgents}
      />
    );

    const messagesButton = screen.getByText('Messages');
    fireEvent.click(messagesButton);

    expect(mockOnFiltersChange).toHaveBeenCalledWith({
      types: ['chat'],
    });
  });

  it('should handle multiple result types', () => {
    const filters: SearchFiltersType = { types: ['message'] };

    render(
      <SearchFilters
        filters={filters}
        onFiltersChange={mockOnFiltersChange}
        games={mockGames}
        agents={mockAgents}
      />
    );

    const chatsButton = screen.getByText('Chats');
    fireEvent.click(chatsButton);

    expect(mockOnFiltersChange).toHaveBeenCalledWith({
      types: ['message', 'chat'],
    });
  });

  it('should clear types array when last type is removed', () => {
    const filters: SearchFiltersType = { types: ['message'] };

    render(
      <SearchFilters
        filters={filters}
        onFiltersChange={mockOnFiltersChange}
        games={mockGames}
        agents={mockAgents}
      />
    );

    const messagesButton = screen.getByText('Messages');
    fireEvent.click(messagesButton);

    expect(mockOnFiltersChange).toHaveBeenCalledWith({
      types: undefined,
    });
  });

  it('should render date inputs', () => {
    render(
      <SearchFilters
        filters={{}}
        onFiltersChange={mockOnFiltersChange}
        games={mockGames}
        agents={mockAgents}
      />
    );

    const dateInputs = screen.getAllByPlaceholderText(/from|to/i);
    expect(dateInputs).toHaveLength(2);
  });

  it('should format and display selected dates', () => {
    const filters: SearchFiltersType = {
      dateFrom: new Date('2024-01-01'),
      dateTo: new Date('2024-12-31'),
    };

    render(
      <SearchFilters
        filters={filters}
        onFiltersChange={mockOnFiltersChange}
        games={mockGames}
        agents={mockAgents}
      />
    );

    const dateInputs = screen.getAllByDisplayValue(/2024/);
    expect(dateInputs.length).toBeGreaterThan(0);
  });

  it('should handle empty games array', () => {
    render(
      <SearchFilters
        filters={{}}
        onFiltersChange={mockOnFiltersChange}
        games={[]}
        agents={mockAgents}
      />
    );

    expect(screen.getByLabelText('Game')).toBeInTheDocument();
  });

  it('should handle empty agents array', () => {
    render(
      <SearchFilters
        filters={{}}
        onFiltersChange={mockOnFiltersChange}
        games={mockGames}
        agents={[]}
      />
    );

    expect(screen.getByLabelText('Agent')).toBeInTheDocument();
  });

  it('should handle missing games and agents props', () => {
    render(<SearchFilters filters={{}} onFiltersChange={mockOnFiltersChange} />);

    expect(screen.getByLabelText('Game')).toBeInTheDocument();
    expect(screen.getByLabelText('Agent')).toBeInTheDocument();
  });
});
