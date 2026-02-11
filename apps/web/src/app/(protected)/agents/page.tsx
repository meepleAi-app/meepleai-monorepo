/**
 * Agent List Page (Issue #4090)
 *
 * Agent catalog with search, filtering, and sorting
 *
 * Features:
 * - Grid view with MeepleCard entity=agent
 * - Search by name/description
 * - Filter by type (Tutor, Arbitro, Decisore)
 * - Sort by usage, rating, name
 */

'use client';

import React, { useState, useMemo } from 'react';
import { Search } from 'lucide-react';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Input } from '@/components/ui/forms/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/primitives/select';

// TODO: Replace with actual API fetch
// import { useQuery } from '@tanstack/react-query';
// import { agentsClient } from '@/lib/api/agents-client';

interface Agent {
  id: string;
  name: string;
  type: 'Tutor' | 'Arbitro' | 'Decisore';
  description: string;
  imageUrl?: string;
  usage: number;
  rating?: number;
}

// Mock data - replace with API call
const mockAgents: Agent[] = [
  {
    id: '1',
    name: 'Tutor Agent',
    type: 'Tutor',
    description: 'Helps you learn game rules and strategies',
    usage: 150,
    rating: 4.5,
  },
  {
    id: '2',
    name: 'Arbitro Agent',
    type: 'Arbitro',
    description: 'Resolves rule conflicts and clarifications',
    usage: 80,
    rating: 4.8,
  },
  {
    id: '3',
    name: 'Decisore Agent',
    type: 'Decisore',
    description: 'Suggests optimal moves and strategies',
    usage: 120,
    rating: 4.6,
  },
];

export default function AgentsPage() {
  const [searchQuery, setSearchQuery] = useState('');
  const [typeFilter, setTypeFilter] = useState<string>('all');
  const [sortBy, setSortBy] = useState<'name' | 'usage' | 'rating'>('usage');

  // TODO: Replace with React Query
  // const { data: agents, isLoading } = useQuery({
  //   queryKey: ['agents'],
  //   queryFn: () => agentsClient.getAll(),
  // });

  const agents = mockAgents;

  // Client-side filtering and sorting
  const filteredAgents = useMemo(() => {
    let result = [...agents];

    // Search filter
    if (searchQuery) {
      result = result.filter(
        agent =>
          agent.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
          agent.description.toLowerCase().includes(searchQuery.toLowerCase())
      );
    }

    // Type filter
    if (typeFilter !== 'all') {
      result = result.filter(agent => agent.type === typeFilter);
    }

    // Sort
    result.sort((a, b) => {
      if (sortBy === 'name') return a.name.localeCompare(b.name);
      if (sortBy === 'usage') return b.usage - a.usage;
      if (sortBy === 'rating') return (b.rating || 0) - (a.rating || 0);
      return 0;
    });

    return result;
  }, [agents, searchQuery, typeFilter, sortBy]);

  return (
    <div className="container mx-auto py-8 px-4">
      {/* Header */}
      <div className="mb-8">
        <h1 className="text-3xl font-bold font-quicksand mb-2">AI Agents</h1>
        <p className="text-muted-foreground">
          Choose an AI agent to help you learn, play, and master board games
        </p>
      </div>

      {/* Filters */}
      <div className="flex flex-col md:flex-row gap-4 mb-6">
        {/* Search */}
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            type="search"
            placeholder="Search agents..."
            value={searchQuery}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) => setSearchQuery(e.target.value)}
            className="pl-10"
            aria-label="Search agents"
          />
        </div>

        {/* Type Filter */}
        <Select value={typeFilter} onValueChange={setTypeFilter}>
          <SelectTrigger className="w-full md:w-48" aria-label="Filter by agent type">
            <SelectValue placeholder="All Types" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Types</SelectItem>
            <SelectItem value="Tutor">Tutor</SelectItem>
            <SelectItem value="Arbitro">Arbitro</SelectItem>
            <SelectItem value="Decisore">Decisore</SelectItem>
          </SelectContent>
        </Select>

        {/* Sort */}
        <Select value={sortBy} onValueChange={(value: string) => setSortBy(value as typeof sortBy)}>
          <SelectTrigger className="w-full md:w-48" aria-label="Sort agents">
            <SelectValue placeholder="Sort by" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="usage">Most Used</SelectItem>
            <SelectItem value="rating">Highest Rated</SelectItem>
            <SelectItem value="name">Name (A-Z)</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Results count */}
      <div className="mb-4 text-sm text-muted-foreground">
        {filteredAgents.length} {filteredAgents.length === 1 ? 'agent' : 'agents'} found
      </div>

      {/* Agent Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
        {filteredAgents.map(agent => (
          <MeepleCard
            key={agent.id}
            entity="agent"
            variant="grid"
            title={agent.name}
            subtitle={agent.description}
            imageUrl={agent.imageUrl}
            rating={agent.rating}
            ratingMax={5}
            metadata={[
              { value: `${agent.usage} uses`, label: 'Usage' },
              { value: agent.type, label: 'Type' },
            ]}
            onClick={() => {
              // Navigate to agent chat or detail
              window.location.href = `/agents/${agent.id}`;
            }}
          />
        ))}
      </div>

      {/* Empty state */}
      {filteredAgents.length === 0 && (
        <div className="text-center py-12">
          <p className="text-muted-foreground mb-4">No agents found</p>
          {searchQuery && (
            <button
              onClick={() => setSearchQuery('')}
              className="text-primary hover:underline"
            >
              Clear search
            </button>
          )}
        </div>
      )}
    </div>
  );
}
