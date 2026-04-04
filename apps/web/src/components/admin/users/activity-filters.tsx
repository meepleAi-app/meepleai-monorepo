'use client';

import { useState } from 'react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';

export interface ActivityFiltersProps {
  onUserSearchChange?: (value: string) => void;
  onActionTypeChange?: (value: string) => void;
  onDateRangeChange?: (value: string) => void;
}

export function ActivityFilters({
  onUserSearchChange,
  onActionTypeChange,
  onDateRangeChange,
}: ActivityFiltersProps) {
  const [userSearch, setUserSearch] = useState('');
  const [actionType, setActionType] = useState('all');
  const [dateRange, setDateRange] = useState('24h');

  const handleUserSearchChange = (value: string) => {
    setUserSearch(value);
    onUserSearchChange?.(value);
  };

  const handleActionTypeChange = (value: string) => {
    setActionType(value);
    onActionTypeChange?.(value);
  };

  const handleDateRangeChange = (value: string) => {
    setDateRange(value);
    onDateRangeChange?.(value);
  };

  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-amber-200/50 dark:border-zinc-700/50 rounded-lg p-4">
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {/* User Search */}
        <div>
          <Label htmlFor="user-search" className="text-sm font-medium mb-2">
            Search User
          </Label>
          <Input
            id="user-search"
            type="text"
            placeholder="Name or email..."
            value={userSearch}
            onChange={e => handleUserSearchChange(e.target.value)}
            className="bg-white dark:bg-zinc-900"
          />
        </div>

        {/* Action Type */}
        <div>
          <Label htmlFor="action-type" className="text-sm font-medium mb-2">
            Action Type
          </Label>
          <Select value={actionType} onValueChange={handleActionTypeChange}>
            <SelectTrigger id="action-type" className="bg-white dark:bg-zinc-900">
              <SelectValue placeholder="Select action type" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Actions</SelectItem>
              <SelectItem value="login">Login</SelectItem>
              <SelectItem value="edit">Edit</SelectItem>
              <SelectItem value="approve">Approve</SelectItem>
              <SelectItem value="config">Config</SelectItem>
              <SelectItem value="delete">Delete</SelectItem>
            </SelectContent>
          </Select>
        </div>

        {/* Date Range */}
        <div>
          <Label htmlFor="date-range" className="text-sm font-medium mb-2">
            Date Range
          </Label>
          <Select value={dateRange} onValueChange={handleDateRangeChange}>
            <SelectTrigger id="date-range" className="bg-white dark:bg-zinc-900">
              <SelectValue placeholder="Select date range" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="24h">Last 24 Hours</SelectItem>
              <SelectItem value="7d">Last 7 Days</SelectItem>
              <SelectItem value="30d">Last 30 Days</SelectItem>
              <SelectItem value="custom">Custom Range</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>
    </div>
  );
}
