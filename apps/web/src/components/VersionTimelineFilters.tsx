import React, { useState } from 'react';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Button } from '@/components/ui/button';

interface TimelineFilters {
  startDate?: string;
  endDate?: string;
  author?: string;
  searchQuery?: string;
}

interface VersionTimelineFiltersProps {
  authors: string[];
  filters: TimelineFilters;
  onFiltersChange: (filters: TimelineFilters) => void;
  onReset: () => void;
}

export const VersionTimelineFilters: React.FC<VersionTimelineFiltersProps> = ({
  authors,
  filters,
  onFiltersChange,
  onReset,
}) => {
  const [localFilters, setLocalFilters] = useState<TimelineFilters>(filters);

  const handleFilterChange = (key: keyof TimelineFilters, value: string | undefined) => {
    const newFilters = {
      ...localFilters,
      [key]: value || undefined,
    };
    setLocalFilters(newFilters);
    onFiltersChange(newFilters);
  };

  const handleReset = () => {
    const emptyFilters: TimelineFilters = {};
    setLocalFilters(emptyFilters);
    onReset();
  };

  return (
    <div className="bg-gray-50 rounded-lg p-4 mb-6">
      <div className="flex flex-wrap gap-4 items-end">
        {/* Date Range */}
        <div className="flex gap-2">
          <div className="flex flex-col">
            <label htmlFor="startDate" className="text-sm font-medium mb-1">
              Start Date
            </label>
            <Input
              id="startDate"
              type="date"
              value={localFilters.startDate || ''}
              onChange={(e) => handleFilterChange('startDate', e.target.value)}
            />
          </div>
          <div className="flex flex-col">
            <label htmlFor="endDate" className="text-sm font-medium mb-1">
              End Date
            </label>
            <Input
              id="endDate"
              type="date"
              value={localFilters.endDate || ''}
              onChange={(e) => handleFilterChange('endDate', e.target.value)}
            />
          </div>
        </div>

        {/* Author Filter */}
        <div className="flex flex-col">
          <label htmlFor="author" className="text-sm font-medium mb-1">
            Author
          </label>
          <Select
            value={localFilters.author || '__all__'}
            onValueChange={(value) => handleFilterChange('author', value === '__all__' ? '' : value)}
          >
            <SelectTrigger id="author" className="w-[180px]">
              <SelectValue placeholder="All authors" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="__all__">All authors</SelectItem>
              {authors.map(author => (
                <SelectItem key={author} value={author}>{author}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Search */}
        <div className="flex flex-col flex-1 min-w-[200px]">
          <label htmlFor="search" className="text-sm font-medium mb-1">
            Search Versions
          </label>
          <Input
            id="search"
            type="text"
            value={localFilters.searchQuery || ''}
            onChange={(e) => handleFilterChange('searchQuery', e.target.value)}
            placeholder="Search by version..."
          />
        </div>

        {/* Reset Button */}
        <Button
          onClick={handleReset}
          variant="secondary"
          aria-label="Reset filters"
        >
          Reset Filters
        </Button>
      </div>
    </div>
  );
};

export default VersionTimelineFilters;
