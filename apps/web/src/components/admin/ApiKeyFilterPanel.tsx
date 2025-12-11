/**
 * ApiKeyFilterPanel Component (Issue #910)
 *
 * Filter panel for API Key management page (/admin/api-keys).
 * Provides comprehensive filtering options for API keys with real-time updates.
 *
 * Features:
 * - Status filter (Active, Expired, Revoked, All)
 * - Scope multi-select (Read, Write, Admin)
 * - Date range filters (Created, Expires)
 * - Search by key name
 * - Last used filter
 * - Clear all filters
 * - WCAG 2.1 AA compliant
 * - Responsive design
 * - Keyboard accessible
 *
 * @example
 * ```tsx
 * <ApiKeyFilterPanel
 *   filters={filters}
 *   onFiltersChange={setFilters}
 *   onReset={() => setFilters({})}
 * />
 * ```
 */

import React from 'react';
import { X, Search, Calendar, Clock, Shield } from 'lucide-react';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import type { ApiKeyFilters, ApiKeyScope, ApiKeyStatus } from '@/types';
import { AVAILABLE_SCOPES, AVAILABLE_STATUSES } from '@/types';

export interface ApiKeyFilterPanelProps {
  /**
   * Current filter state
   */
  filters: ApiKeyFilters;

  /**
   * Callback when filters change
   */
  onFiltersChange: (filters: ApiKeyFilters) => void;

  /**
   * Optional callback for reset action
   */
  onReset?: () => void;

  /**
   * Optional CSS classes
   */
  className?: string;
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

/**
 * ApiKeyFilterPanel component
 */
export const ApiKeyFilterPanel: React.FC<ApiKeyFilterPanelProps> = ({
  filters,
  onFiltersChange,
  onReset,
  className = '',
}) => {
  const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    onFiltersChange({
      ...filters,
      search: e.target.value || undefined,
    });
  };

  const handleStatusChange = (value: string) => {
    onFiltersChange({
      ...filters,
      status: value === 'all' ? undefined : (value as ApiKeyStatus),
    });
  };

  const handleScopeToggle = (scope: ApiKeyScope) => {
    const currentScopes = filters.scopes || [];
    const newScopes = currentScopes.includes(scope)
      ? currentScopes.filter(s => s !== scope)
      : [...currentScopes, scope];

    onFiltersChange({
      ...filters,
      scopes: newScopes.length === 0 ? undefined : newScopes,
    });
  };

  const handleCreatedFromChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    onFiltersChange({
      ...filters,
      createdFrom: parseDateFromInput(e.target.value),
    });
  };

  const handleCreatedToChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    onFiltersChange({
      ...filters,
      createdTo: parseDateFromInput(e.target.value),
    });
  };

  const handleExpiresFromChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    onFiltersChange({
      ...filters,
      expiresFrom: parseDateFromInput(e.target.value),
    });
  };

  const handleExpiresToChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    onFiltersChange({
      ...filters,
      expiresTo: parseDateFromInput(e.target.value),
    });
  };

  const handleLastUsedChange = (value: string) => {
    onFiltersChange({
      ...filters,
      lastUsedDays: value === 'all' ? undefined : parseInt(value, 10),
    });
  };

  const handleClearFilters = () => {
    if (onReset) {
      onReset();
    } else {
      onFiltersChange({});
    }
  };

  const hasActiveFilters =
    filters.search ||
    filters.status ||
    (filters.scopes && filters.scopes.length > 0) ||
    filters.createdFrom ||
    filters.createdTo ||
    filters.expiresFrom ||
    filters.expiresTo ||
    filters.lastUsedDays;

  const getScopeColor = (scope: ApiKeyScope): string => {
    const option = AVAILABLE_SCOPES.find(s => s.value === scope);
    if (!option) return 'default';

    const colorMap = {
      blue: 'bg-blue-100 text-blue-800 hover:bg-blue-200',
      green: 'bg-green-100 text-green-800 hover:bg-green-200',
      red: 'bg-red-100 text-red-800 hover:bg-red-200',
    };

    return colorMap[option.color];
  };

  return (
    <div className={`p-4 space-y-4 bg-muted/50 rounded-lg border ${className}`}>
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold flex items-center gap-2">
          <Shield className="w-4 h-4" />
          Filters
        </h3>
        {hasActiveFilters && (
          <Button
            variant="ghost"
            size="sm"
            onClick={handleClearFilters}
            className="h-7 text-xs"
            aria-label="Clear all filters"
          >
            <X className="w-3 h-3 mr-1" />
            Clear All
          </Button>
        )}
      </div>

      {/* Search */}
      <div className="space-y-2">
        <Label htmlFor="filter-search" className="text-xs flex items-center gap-1">
          <Search className="w-3 h-3" />
          Search by Name
        </Label>
        <Input
          id="filter-search"
          type="text"
          value={filters.search || ''}
          onChange={handleSearchChange}
          placeholder="Enter key name..."
          className="h-8 text-xs"
          aria-label="Search API keys by name"
        />
      </div>

      {/* Status Filter */}
      <div className="space-y-2">
        <Label htmlFor="filter-status" className="text-xs">
          Status
        </Label>
        <Select value={filters.status || 'all'} onValueChange={handleStatusChange}>
          <SelectTrigger id="filter-status" className="h-8 text-xs">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            {AVAILABLE_STATUSES.map(status => (
              <SelectItem key={status.value} value={status.value}>
                {status.label}
                {status.description && (
                  <span className="text-muted-foreground text-[10px] ml-1">
                    ({status.description})
                  </span>
                )}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Scope Multi-Select */}
      <div className="space-y-2">
        <Label className="text-xs flex items-center gap-1">
          <Shield className="w-3 h-3" />
          Scopes
        </Label>
        <div className="flex flex-wrap gap-1">
          {AVAILABLE_SCOPES.map(({ value, label, description }) => {
            const isSelected = filters.scopes?.includes(value);
            return (
              <button
                key={value}
                onClick={() => handleScopeToggle(value)}
                className={`
                  px-2 py-1 text-xs rounded-md border transition-all
                  ${isSelected ? getScopeColor(value) : 'bg-background hover:bg-muted border-input'}
                `}
                title={description}
                aria-label={`Filter by ${label} scope`}
                aria-pressed={isSelected}
              >
                {label}
              </button>
            );
          })}
        </div>
      </div>

      {/* Created Date Range */}
      <div className="space-y-2">
        <Label className="text-xs flex items-center gap-1">
          <Calendar className="w-3 h-3" />
          Created Date
        </Label>
        <div className="grid grid-cols-2 gap-2">
          <input
            type="date"
            value={formatDateForInput(filters.createdFrom)}
            onChange={handleCreatedFromChange}
            className="h-8 px-2 text-xs rounded-md border border-input bg-background"
            placeholder="From"
            aria-label="Created from date"
          />
          <input
            type="date"
            value={formatDateForInput(filters.createdTo)}
            onChange={handleCreatedToChange}
            className="h-8 px-2 text-xs rounded-md border border-input bg-background"
            placeholder="To"
            aria-label="Created to date"
          />
        </div>
      </div>

      {/* Expires Date Range */}
      <div className="space-y-2">
        <Label className="text-xs flex items-center gap-1">
          <Calendar className="w-3 h-3" />
          Expires Date
        </Label>
        <div className="grid grid-cols-2 gap-2">
          <input
            type="date"
            value={formatDateForInput(filters.expiresFrom)}
            onChange={handleExpiresFromChange}
            className="h-8 px-2 text-xs rounded-md border border-input bg-background"
            placeholder="From"
            aria-label="Expires from date"
          />
          <input
            type="date"
            value={formatDateForInput(filters.expiresTo)}
            onChange={handleExpiresToChange}
            className="h-8 px-2 text-xs rounded-md border border-input bg-background"
            placeholder="To"
            aria-label="Expires to date"
          />
        </div>
      </div>

      {/* Last Used Filter */}
      <div className="space-y-2">
        <Label htmlFor="filter-last-used" className="text-xs flex items-center gap-1">
          <Clock className="w-3 h-3" />
          Last Used
        </Label>
        <Select
          value={filters.lastUsedDays?.toString() || 'all'}
          onValueChange={handleLastUsedChange}
        >
          <SelectTrigger id="filter-last-used" className="h-8 text-xs">
            <SelectValue placeholder="Any time" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Any time</SelectItem>
            <SelectItem value="7">Last 7 days</SelectItem>
            <SelectItem value="30">Last 30 days</SelectItem>
            <SelectItem value="90">Last 90 days</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Active Filters Summary */}
      {hasActiveFilters && (
        <div className="pt-2 border-t space-y-2">
          <Label className="text-xs text-muted-foreground">Active Filters</Label>
          <div className="flex flex-wrap gap-1">
            {filters.search && (
              <Badge variant="secondary" className="text-[10px]">
                Search: {filters.search}
              </Badge>
            )}
            {filters.status && filters.status !== 'all' && (
              <Badge variant="secondary" className="text-[10px]">
                Status: {AVAILABLE_STATUSES.find(s => s.value === filters.status)?.label}
              </Badge>
            )}
            {filters.scopes && filters.scopes.length > 0 && (
              <Badge variant="secondary" className="text-[10px]">
                Scopes: {filters.scopes.length}
              </Badge>
            )}
            {(filters.createdFrom || filters.createdTo) && (
              <Badge variant="secondary" className="text-[10px]">
                Created: {filters.createdFrom ? '✓' : ''}
                {filters.createdFrom && filters.createdTo ? '-' : ''}
                {filters.createdTo ? '✓' : ''}
              </Badge>
            )}
            {(filters.expiresFrom || filters.expiresTo) && (
              <Badge variant="secondary" className="text-[10px]">
                Expires: {filters.expiresFrom ? '✓' : ''}
                {filters.expiresFrom && filters.expiresTo ? '-' : ''}
                {filters.expiresTo ? '✓' : ''}
              </Badge>
            )}
            {filters.lastUsedDays && (
              <Badge variant="secondary" className="text-[10px]">
                Used: Last {filters.lastUsedDays}d
              </Badge>
            )}
          </div>
        </div>
      )}
    </div>
  );
};

export default ApiKeyFilterPanel;
