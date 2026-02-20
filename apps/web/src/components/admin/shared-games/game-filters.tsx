'use client';

import { useState } from 'react';

import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

export interface GameFiltersProps {
  onSearchChange?: (value: string) => void;
  onCategoryChange?: (value: string) => void;
  onStatusChange?: (value: string) => void;
  onPlayersChange?: (value: string) => void;
}

export function GameFilters({
  onSearchChange,
  onCategoryChange,
  onStatusChange,
  onPlayersChange,
}: GameFiltersProps) {
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState('all');
  const [status, setStatus] = useState('all');
  const [players, setPlayers] = useState('all');

  const handleSearchChange = (value: string) => {
    setSearch(value);
    onSearchChange?.(value);
  };

  const handleCategoryChange = (value: string) => {
    setCategory(value);
    onCategoryChange?.(value);
  };

  const handleStatusChange = (value: string) => {
    setStatus(value);
    onStatusChange?.(value);
  };

  const handlePlayersChange = (value: string) => {
    setPlayers(value);
    onPlayersChange?.(value);
  };

  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-amber-200/50 dark:border-zinc-700/50 rounded-lg p-4">
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        {/* Search */}
        <div className="md:col-span-2">
          <Label htmlFor="game-search" className="text-sm font-medium mb-2">
            Search Games
          </Label>
          <Input
            id="game-search"
            type="text"
            placeholder="Search by title, publisher..."
            value={search}
            onChange={(e) => handleSearchChange(e.target.value)}
            className="bg-white dark:bg-zinc-900"
          />
        </div>

        {/* Category */}
        <div>
          <Label htmlFor="category" className="text-sm font-medium mb-2">
            Category
          </Label>
          <Select value={category} onValueChange={handleCategoryChange}>
            <SelectTrigger id="category" className="bg-white dark:bg-zinc-900">
              <SelectValue placeholder="Select category" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Categories</SelectItem>
              <SelectItem value="strategy">Strategy</SelectItem>
              <SelectItem value="party">Party</SelectItem>
              <SelectItem value="cooperative">Cooperative</SelectItem>
              <SelectItem value="deck-building">Deck Building</SelectItem>
            </SelectContent>
          </Select>
        </div>

        {/* Status */}
        <div>
          <Label htmlFor="status" className="text-sm font-medium mb-2">
            Status
          </Label>
          <Select value={status} onValueChange={handleStatusChange}>
            <SelectTrigger id="status" className="bg-white dark:bg-zinc-900">
              <SelectValue placeholder="Select status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Status</SelectItem>
              <SelectItem value="published">Published</SelectItem>
              <SelectItem value="draft">Draft</SelectItem>
              <SelectItem value="archived">Archived</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>

      {/* Advanced Filters Row */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mt-4">
        {/* Player Count */}
        <div>
          <Label htmlFor="players" className="text-sm font-medium mb-2">
            Player Count
          </Label>
          <Select value={players} onValueChange={handlePlayersChange}>
            <SelectTrigger id="players" className="bg-white dark:bg-zinc-900">
              <SelectValue placeholder="Any player count" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Any</SelectItem>
              <SelectItem value="1-2">1-2 Players</SelectItem>
              <SelectItem value="3-4">3-4 Players</SelectItem>
              <SelectItem value="5+">5+ Players</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>
    </div>
  );
}
