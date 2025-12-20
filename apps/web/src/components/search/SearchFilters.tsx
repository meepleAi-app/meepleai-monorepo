/**
 * SearchFilters Component
 * Filter UI for advanced search (Issue #1101)
 */

import React from 'react';

import { X, Calendar } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import type { SearchFilters as SearchFiltersType, SearchResultType } from '@/types';
import type { Game, Agent } from '@/types';

interface SearchFiltersProps {
  filters: SearchFiltersType;
  onFiltersChange: (filters: SearchFiltersType) => void;
  games?: Game[];
  agents?: Agent[];
}

/**
 * Format date for input[type="date"]
 */
function formatDateForInput(date?: Date): string {
  if (!date) return '';
  return date.toISOString().split('T')[0];
}

/**
 * Parse date from input[type="date"]
 */
function parseDateFromInput(value: string): Date | undefined {
  if (!value) return undefined;
  return new Date(value);
}

export const SearchFilters: React.FC<SearchFiltersProps> = ({
  filters,
  onFiltersChange,
  games = [],
  agents = [],
}) => {
  const handleGameChange = (value: string) => {
    onFiltersChange({
      ...filters,
      gameId: value === 'all' ? undefined : value,
    });
  };

  const handleAgentChange = (value: string) => {
    onFiltersChange({
      ...filters,
      agentId: value === 'all' ? undefined : value,
    });
  };

  const handleTypeToggle = (type: SearchResultType) => {
    const currentTypes = filters.types || [];
    const newTypes = currentTypes.includes(type)
      ? currentTypes.filter(t => t !== type)
      : [...currentTypes, type];

    onFiltersChange({
      ...filters,
      types: newTypes.length === 0 ? undefined : newTypes,
    });
  };

  const handleDateFromChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    onFiltersChange({
      ...filters,
      dateFrom: parseDateFromInput(e.target.value),
    });
  };

  const handleDateToChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    onFiltersChange({
      ...filters,
      dateTo: parseDateFromInput(e.target.value),
    });
  };

  const handleClearFilters = () => {
    onFiltersChange({});
  };

  const hasActiveFilters =
    filters.gameId ||
    filters.agentId ||
    filters.dateFrom ||
    filters.dateTo ||
    (filters.types && filters.types.length > 0);

  const resultTypes: { value: SearchResultType; label: string }[] = [
    { value: 'message', label: 'Messages' },
    { value: 'chat', label: 'Chats' },
    { value: 'game', label: 'Games' },
    { value: 'agent', label: 'Agents' },
    { value: 'pdf', label: 'PDFs' },
  ];

  return (
    <div className="p-4 space-y-4 bg-muted/50">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold">Filters</h3>
        {hasActiveFilters && (
          <Button variant="ghost" size="sm" onClick={handleClearFilters} className="h-7 text-xs">
            <X className="w-3 h-3 mr-1" />
            Clear All
          </Button>
        )}
      </div>

      <div className="grid grid-cols-2 gap-3">
        {/* Game Filter */}
        <div className="space-y-2">
          <Label htmlFor="filter-game" className="text-xs">
            Game
          </Label>
          <Select value={filters.gameId || 'all'} onValueChange={handleGameChange}>
            <SelectTrigger id="filter-game" className="h-8 text-xs">
              <SelectValue placeholder="All games" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All games</SelectItem>
              {games.map(game => (
                <SelectItem key={game.id} value={game.id}>
                  {game.title}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Agent Filter */}
        <div className="space-y-2">
          <Label htmlFor="filter-agent" className="text-xs">
            Agent
          </Label>
          <Select value={filters.agentId || 'all'} onValueChange={handleAgentChange}>
            <SelectTrigger id="filter-agent" className="h-8 text-xs">
              <SelectValue placeholder="All agents" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All agents</SelectItem>
              {agents.map(agent => (
                <SelectItem key={agent.id} value={agent.id}>
                  {agent.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      {/* Date Range */}
      <div className="space-y-2">
        <Label className="text-xs flex items-center gap-1">
          <Calendar className="w-3 h-3" />
          Date Range
        </Label>
        <div className="grid grid-cols-2 gap-2">
          <input
            type="date"
            value={formatDateForInput(filters.dateFrom)}
            onChange={handleDateFromChange}
            className="h-8 px-2 text-xs rounded-md border border-input bg-background"
            placeholder="From"
            aria-label="Search from date"
          />
          <input
            type="date"
            value={formatDateForInput(filters.dateTo)}
            onChange={handleDateToChange}
            className="h-8 px-2 text-xs rounded-md border border-input bg-background"
            placeholder="To"
            aria-label="Search to date"
          />
        </div>
      </div>

      {/* Result Type Filter */}
      <div className="space-y-2">
        <Label className="text-xs">Result Types</Label>
        <div className="flex flex-wrap gap-1">
          {resultTypes.map(({ value, label }) => (
            <Button
              key={value}
              variant={filters.types?.includes(value) ? 'default' : 'outline'}
              size="sm"
              onClick={() => handleTypeToggle(value)}
              className="h-7 text-xs"
            >
              {label}
            </Button>
          ))}
        </div>
      </div>

      {/* TODO: Issue #2029 - PDF Language Filter (requires backend support) */}
      {/* <div className="space-y-2">
        <Label htmlFor="filter-language" className="text-xs">
          PDF Language
        </Label>
        <Select disabled>
          <SelectTrigger id="filter-language" className="h-8 text-xs">
            <SelectValue placeholder="All languages (coming soon)" />
          </SelectTrigger>
        </Select>
      </div> */}
    </div>
  );
};
