/**
 * Agent List Page (Issue #4090)
 *
 * Agent catalog with search, filtering, and sorting
 *
 * Features:
 * - Grid view with MeepleCard entity=agent
 * - Search by name/description
 * - Filter by type (Tutor, Arbitro, Stratega, Narratore)
 * - Sort by usage, rating, name
 */

'use client';

import React, { useState, useMemo } from 'react';

import { Bot, Plus, Search } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { AgentCreationSheet } from '@/components/agent/config';
import {
  ListPageHeader,
  useViewPreference,
} from '@/components/ui/data-display/ListPageHeader';
import { MeepleCard, entityColors } from '@/components/ui/data-display/meeple-card';
import { useCardBrowser, type CardRef } from '@/components/ui/data-display/meeple-card-browser';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { getNavigationLinks } from '@/config/entity-navigation';
import { useAgents } from '@/hooks/queries/useAgents';
import { useAgentSlots } from '@/hooks/queries/useAgentSlots';
import { useEntityActions } from '@/hooks/use-entity-actions';

import { AgentsNavConfig } from './NavConfig';

/** Agent card wrapper to use entity actions hook per-card */
function AgentCard({
  agent,
  onClick,
}: {
  agent: { id: string; name: string; type: string; invocationCount: number; strategyName: string };
  onClick: () => void;
}) {
  const entityActions = useEntityActions({ entity: 'agent', id: agent.id });

  return (
    <MeepleCard
      entity="agent"
      variant="grid"
      title={agent.name}
      subtitle={`${agent.type} agent`}
      metadata={[
        { value: `${agent.invocationCount} uses`, label: 'Usage' },
        { value: agent.strategyName, label: 'Strategy' },
      ]}
      navigateTo={getNavigationLinks('agent', { id: agent.id })}
      entityQuickActions={entityActions.quickActions}
      showInfoButton
      infoHref={`/agents/${agent.id}`}
      infoTooltip="Dettagli agent"
      onClick={onClick}
    />
  );
}

export default function AgentsPage() {
  const router = useRouter();
  const [searchQuery, setSearchQuery] = useState('');
  const [typeFilter, setTypeFilter] = useState<string>('all');
  const [sortBy, setSortBy] = useState<'name' | 'usage' | 'rating'>('usage');
  const [creationSheetOpen, setCreationSheetOpen] = useState(false);
  const { open: openBrowser } = useCardBrowser();
  const [viewMode, setViewMode] = useViewPreference('agents');

  // Use real API (Issue #4126)
  const { data: agents = [], isLoading: _isLoading } = useAgents({
    activeOnly: true,
    type: typeFilter === 'all' ? undefined : typeFilter,
  });

  // Issue #4778: Slot availability
  const { data: slotsData } = useAgentSlots();

  // Client-side filtering and sorting
  const filteredAgents = useMemo(() => {
    let result = [...agents];

    // Search filter
    if (searchQuery) {
      result = result.filter(
        agent =>
          agent.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
          agent.type.toLowerCase().includes(searchQuery.toLowerCase())
      );
    }

    // Type filter
    if (typeFilter !== 'all') {
      result = result.filter(agent => agent.type === typeFilter);
    }

    // Sort
    result.sort((a, b) => {
      if (sortBy === 'name') return a.name.localeCompare(b.name);
      if (sortBy === 'usage') return b.invocationCount - a.invocationCount;
      if (sortBy === 'rating') return 0; // Rating TBD
      return 0;
    });

    return result;
  }, [agents, searchQuery, typeFilter, sortBy]);

  const cardRefs: CardRef[] = useMemo(
    () =>
      filteredAgents.map(agent => ({
        id: agent.id,
        entity: 'agent' as const,
        title: agent.name,
        subtitle: `${agent.type} agent`,
        color: entityColors.agent.hsl,
      })),
    [filteredAgents]
  );

  return (
    <div className="container mx-auto py-8 px-4">
      <AgentsNavConfig />
      {/* Header */}
      <div className="mb-8 flex flex-col sm:flex-row sm:items-start sm:justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold font-quicksand mb-2">AI Agents</h1>
          <p className="text-muted-foreground">
            Choose an AI agent to help you learn, play, and master board games
          </p>
          {/* Issue #4778: Slot indicator */}
          {slotsData && (
            <div className="mt-3 flex items-center gap-3">
              <div className="flex-1 max-w-[200px] h-2 bg-muted rounded-full overflow-hidden">
                <div
                  className="h-full bg-amber-500 rounded-full transition-all"
                  style={{
                    width: `${slotsData.total > 0 ? (slotsData.used / slotsData.total) * 100 : 0}%`,
                  }}
                />
              </div>
              <span className="text-xs text-muted-foreground">
                {slotsData.used} / {slotsData.total} slot usati
              </span>
            </div>
          )}
        </div>
        <Button
          onClick={() => setCreationSheetOpen(true)}
          disabled={slotsData?.available === 0}
          className="shrink-0"
        >
          <Plus className="h-4 w-4 mr-2" />
          Crea Agente
        </Button>
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
            <SelectItem value="Stratega">Stratega</SelectItem>
            <SelectItem value="Narratore">Narratore</SelectItem>
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

      {/* View toggle + count */}
      <ListPageHeader
        title="AI Agents"
        count={filteredAgents.length}
        viewMode={viewMode}
        onViewModeChange={setViewMode}
        filters={[
          { key: 'all', label: 'All' },
          { key: 'Tutor', label: 'Tutor' },
          { key: 'Arbitro', label: 'Arbitro' },
          { key: 'Stratega', label: 'Stratega' },
          { key: 'Narratore', label: 'Narratore' },
        ]}
        activeFilter={typeFilter}
        onFilterChange={setTypeFilter}
      />

      {/* Agent Grid / List */}
      <div
        className={
          viewMode === 'grid'
            ? 'grid grid-cols-2 md:grid-cols-3 xl:grid-cols-4 gap-3 md:gap-6 mt-4'
            : 'flex flex-col gap-3 mt-4'
        }
        data-testid="card-grid"
      >
        {filteredAgents.map((agent, index) => (
          <AgentCard
            key={agent.id}
            agent={agent}
            onClick={() => {
              if (window.innerWidth < 768) {
                openBrowser(cardRefs, index);
              } else {
                router.push(`/agents/${agent.id}`);
              }
            }}
          />
        ))}
      </div>

      {/* Empty state */}
      {filteredAgents.length === 0 && (
        <div className="text-center py-12">
          <Bot className="h-12 w-12 mx-auto mb-4 text-muted-foreground/50" />
          <p className="text-muted-foreground mb-4">
            {searchQuery ? 'No agents found' : 'No agents yet. Create your first AI agent!'}
          </p>
          {searchQuery ? (
            <button onClick={() => setSearchQuery('')} className="text-primary hover:underline">
              Clear search
            </button>
          ) : (
            <Button
              onClick={() => setCreationSheetOpen(true)}
              disabled={slotsData?.available === 0}
              variant="outline"
            >
              <Plus className="h-4 w-4 mr-2" />
              Crea il tuo primo agente
            </Button>
          )}
        </div>
      )}

      {/* Issue #4778: Agent creation wizard */}
      <AgentCreationSheet isOpen={creationSheetOpen} onClose={() => setCreationSheetOpen(false)} />
    </div>
  );
}
