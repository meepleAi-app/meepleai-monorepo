import React, { useState } from 'react';

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
            <label htmlFor="startDate" className="text-sm font-medium text-gray-700 mb-1">
              Start Date
            </label>
            <input
              id="startDate"
              type="date"
              value={localFilters.startDate || ''}
              onChange={(e) => handleFilterChange('startDate', e.target.value)}
              className="px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div className="flex flex-col">
            <label htmlFor="endDate" className="text-sm font-medium text-gray-700 mb-1">
              End Date
            </label>
            <input
              id="endDate"
              type="date"
              value={localFilters.endDate || ''}
              onChange={(e) => handleFilterChange('endDate', e.target.value)}
              className="px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </div>

        {/* Author Filter */}
        <div className="flex flex-col">
          <label htmlFor="author" className="text-sm font-medium text-gray-700 mb-1">
            Author
          </label>
          <select
            id="author"
            value={localFilters.author || ''}
            onChange={(e) => handleFilterChange('author', e.target.value)}
            className="px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="">All authors</option>
            {authors.map(author => (
              <option key={author} value={author}>{author}</option>
            ))}
          </select>
        </div>

        {/* Search */}
        <div className="flex flex-col flex-1 min-w-[200px]">
          <label htmlFor="search" className="text-sm font-medium text-gray-700 mb-1">
            Search Versions
          </label>
          <input
            id="search"
            type="text"
            value={localFilters.searchQuery || ''}
            onChange={(e) => handleFilterChange('searchQuery', e.target.value)}
            placeholder="Search by version..."
            className="px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        {/* Reset Button */}
        <button
          onClick={handleReset}
          className="px-4 py-2 bg-gray-200 text-gray-700 rounded-md hover:bg-gray-300 focus:outline-none focus:ring-2 focus:ring-gray-400 transition-colors"
          aria-label="Reset filters"
        >
          Reset Filters
        </button>
      </div>
    </div>
  );
};

export default VersionTimelineFilters;
