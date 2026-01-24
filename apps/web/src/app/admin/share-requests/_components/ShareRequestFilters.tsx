import { Input } from '@/components/ui';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui';
import { Search } from 'lucide-react';
import type {
  ShareRequestStatus,
  ContributionType,
  ShareRequestSortField,
  SortDirection,
} from '@/lib/api/schemas/admin-share-requests.schemas';

/**
 * Share Request Filters Component
 *
 * Provides filtering and sorting controls for admin share requests queue:
 * - Search by game title or contributor name
 * - Filter by status (Pending, InReview, etc.)
 * - Filter by contribution type (NewGame, AdditionalContent)
 * - Sort by field (CreatedAt, GameTitle, ContributorName, Status)
 * - Sort direction (Ascending, Descending)
 *
 * Issue #2745: Frontend - Admin Review Interface
 */

interface ShareRequestFiltersProps {
  searchTerm: string;
  status: ShareRequestStatus | 'all';
  contributionType: ContributionType | 'all';
  sortBy: ShareRequestSortField;
  sortDirection: SortDirection;
  onSearchChange: (value: string) => void;
  onStatusChange: (value: ShareRequestStatus | 'all') => void;
  onContributionTypeChange: (value: ContributionType | 'all') => void;
  onSortByChange: (value: ShareRequestSortField) => void;
  onSortDirectionChange: (value: SortDirection) => void;
}

export function ShareRequestFilters({
  searchTerm,
  status,
  contributionType,
  sortBy,
  sortDirection,
  onSearchChange,
  onStatusChange,
  onContributionTypeChange,
  onSortByChange,
  onSortDirectionChange,
}: ShareRequestFiltersProps){
  return (
    <div className="space-y-4">
      {/* Search */}
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <Input
          placeholder="Search by game title or contributor name..."
          value={searchTerm}
          onChange={(e) => onSearchChange(e.target.value)}
          className="pl-9"
        />
      </div>

      {/* Filters Row */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-3">
        {/* Status Filter */}
        <Select value={status} onValueChange={onStatusChange}>
          <SelectTrigger>
            <SelectValue placeholder="All Statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Statuses</SelectItem>
            <SelectItem value="Pending">Pending Review</SelectItem>
            <SelectItem value="InReview">In Review</SelectItem>
            <SelectItem value="ChangesRequested">Changes Requested</SelectItem>
            <SelectItem value="Approved">Approved</SelectItem>
            <SelectItem value="Rejected">Rejected</SelectItem>
            <SelectItem value="Withdrawn">Withdrawn</SelectItem>
          </SelectContent>
        </Select>

        {/* Contribution Type Filter */}
        <Select value={contributionType} onValueChange={onContributionTypeChange}>
          <SelectTrigger>
            <SelectValue placeholder="All Types" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Types</SelectItem>
            <SelectItem value="NewGame">New Game</SelectItem>
            <SelectItem value="AdditionalContent">Additional Content</SelectItem>
          </SelectContent>
        </Select>

        {/* Sort By */}
        <Select value={sortBy} onValueChange={onSortByChange}>
          <SelectTrigger>
            <SelectValue placeholder="Sort By" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="CreatedAt">Created Date</SelectItem>
            <SelectItem value="GameTitle">Game Title</SelectItem>
            <SelectItem value="ContributorName">Contributor Name</SelectItem>
            <SelectItem value="Status">Status</SelectItem>
          </SelectContent>
        </Select>

        {/* Sort Direction */}
        <Select value={sortDirection} onValueChange={onSortDirectionChange}>
          <SelectTrigger>
            <SelectValue placeholder="Direction" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="Ascending">Oldest First</SelectItem>
            <SelectItem value="Descending">Newest First</SelectItem>
          </SelectContent>
        </Select>
      </div>
    </div>
  );
}
