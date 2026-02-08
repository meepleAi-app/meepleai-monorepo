'use client';

import { Search } from 'lucide-react';
import { useState } from 'react';
import { Button, Input, Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui';

interface BuilderFiltersProps {
  onFilterChange: (filters: { activeOnly: boolean; search: string }) => void;
}

export function BuilderFilters({ onFilterChange }: BuilderFiltersProps) {
  const [search, setSearch] = useState('');
  const [activeOnly, setActiveOnly] = useState<string>('all');

  const handleApply = () => {
    onFilterChange({
      activeOnly: activeOnly === 'active',
      search: search.trim(),
    });
  };

  return (
    <div className="flex gap-4 items-end">
      <div className="flex-1">
        <label className="text-sm font-medium mb-2 block">Search</label>
        <Input
          placeholder="Search by name or description..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleApply()}
        />
      </div>

      <div className="w-48">
        <label className="text-sm font-medium mb-2 block">Status</label>
        <Select value={activeOnly} onValueChange={setActiveOnly}>
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All</SelectItem>
            <SelectItem value="active">Active Only</SelectItem>
            <SelectItem value="inactive">Inactive Only</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <Button onClick={handleApply}>
        <Search className="h-4 w-4 mr-2" />
        Apply Filters
      </Button>
    </div>
  );
}
