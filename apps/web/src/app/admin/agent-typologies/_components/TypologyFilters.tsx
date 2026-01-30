/**
 * Typology Filters Component (Issue #3179)
 *
 * Filter controls for agent typologies table.
 * Includes status filter, created by filter, and search input.
 */

import { useState, useEffect, useMemo } from 'react';

import { Search } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import type { AgentTypology } from '@/lib/api/clients/adminClient';

interface TypologyFiltersProps {
  status: string;
  onStatusChange: (status: string) => void;
  createdBy: string;
  onCreatedByChange: (createdBy: string) => void;
  search: string;
  onSearchChange: (search: string) => void;
  typologies: AgentTypology[];
}

export function TypologyFilters({
  status,
  onStatusChange,
  createdBy,
  onCreatedByChange,
  search,
  onSearchChange,
  typologies,
}: TypologyFiltersProps) {
  const [searchInput, setSearchInput] = useState(search);

  // Debounce search input
  useEffect(() => {
    const timer = setTimeout(() => {
      onSearchChange(searchInput);
    }, 300);

    return () => clearTimeout(timer);
  }, [searchInput, onSearchChange]);

  // Extract unique creators from typologies
  const creators = useMemo(() => {
    const uniqueCreators = new Set(
      typologies.map(t => JSON.stringify({ id: t.createdBy, name: t.createdByDisplayName }))
    );
    return Array.from(uniqueCreators)
      .map(json => JSON.parse(json) as { id: string; name: string })
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [typologies]);

  const handleSearch = () => {
    onSearchChange(searchInput);
  };

  return (
    <div className="flex items-center gap-4 flex-wrap">
      {/* Search */}
      <div className="flex items-center gap-2 flex-1 min-w-[300px]">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Search by name or description..."
            className="pl-10"
            value={searchInput}
            onChange={e => setSearchInput(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleSearch()}
          />
        </div>
        <Button variant="secondary" onClick={handleSearch}>
          Search
        </Button>
      </div>

      {/* Status Filter */}
      <div className="flex items-center gap-2">
        <Label htmlFor="status-filter" className="shrink-0">
          Status:
        </Label>
        <Select value={status} onValueChange={onStatusChange}>
          <SelectTrigger id="status-filter" className="w-[180px]">
            <SelectValue placeholder="All" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="All">All</SelectItem>
            <SelectItem value="Draft">Draft</SelectItem>
            <SelectItem value="PendingReview">Pending Review</SelectItem>
            <SelectItem value="Approved">Approved</SelectItem>
            <SelectItem value="Rejected">Rejected</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Created By Filter */}
      <div className="flex items-center gap-2">
        <Label htmlFor="creator-filter" className="shrink-0">
          Created By:
        </Label>
        <Select value={createdBy} onValueChange={onCreatedByChange}>
          <SelectTrigger id="creator-filter" className="w-[200px]">
            <SelectValue placeholder="All" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="All">All</SelectItem>
            {creators.map(creator => (
              <SelectItem key={creator.id} value={creator.id}>
                {creator.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
    </div>
  );
}
