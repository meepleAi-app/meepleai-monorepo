/**
 * UserActivityTimeline Component - Issue #911
 *
 * Displays user activity audit log timeline with filtering capabilities.
 * Reuses ActivityFeed component for consistent UI.
 *
 * Features:
 * - Fetches user activity from backend (admin or own profile)
 * - Configurable filters (action type, resource, date range)
 * - Real-time loading states and error handling
 * - Responsive layout with Shadcn/UI components
 */

import { useState, useEffect, useCallback } from 'react';

import { Loader2, RefreshCw, Filter, X } from 'lucide-react';

import { ActivityFeed, type ActivityEvent } from '@/components/admin/ActivityFeed';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/data-display/card';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import {
  Select,
  SelectTrigger,
  SelectValue,
  SelectContent,
  SelectItem,
} from '@/components/ui/overlays/select';
import { api } from '@/lib/api';
import type { UserActivityDto, UserActivityFilters } from '@/lib/api/schemas';

export interface UserActivityTimelineProps {
  /** User ID to fetch activity for. If null, fetches current user's activity */
  userId?: string | null;
  /** Maximum number of activities to display */
  maxEvents?: number;
  /** Show filter controls */
  showFilters?: boolean;
  /** Show view all link */
  showViewAll?: boolean;
  /** View all link href */
  viewAllHref?: string;
  /** Custom class name */
  className?: string;
  /** Enable auto-refresh (interval in ms, 0 to disable) */
  autoRefreshMs?: number;
}

const ACTION_FILTER_OPTIONS = [
  { value: 'all', label: 'Tutte le azioni' },
  { value: 'Login,Logout', label: 'Autenticazione' },
  { value: 'PasswordChanged,PasswordReset', label: 'Password' },
  { value: 'TwoFactorEnabled,TwoFactorDisabled', label: '2FA' },
  { value: 'ApiKeyCreated,ApiKeyRevoked', label: 'API Keys' },
  { value: 'ProfileUpdated,EmailChanged', label: 'Profilo' },
];

const RESOURCE_FILTER_OPTIONS = [
  { value: 'all', label: 'Tutte le risorse' },
  { value: 'User', label: 'Utente' },
  { value: 'Session', label: 'Sessione' },
  { value: 'ApiKey', label: 'API Key' },
  { value: 'Game', label: 'Gioco' },
  { value: 'PDF', label: 'PDF' },
];

function mapActivityToEvent(activity: UserActivityDto): ActivityEvent {
  const severity =
    activity.result === 'Success' ? 'Info' : activity.result === 'Failed' ? 'Error' : 'Warning';

  return {
    id: activity.id,
    eventType: activity.action,
    description: activity.details || `${activity.action} on ${activity.resource}`,
    userId: null,
    userEmail: activity.ipAddress || undefined,
    entityId: activity.resourceId,
    entityType: activity.resource,
    timestamp: activity.createdAt,
    severity,
  };
}

export function UserActivityTimeline({
  userId = null,
  maxEvents = 50,
  showFilters = true,
  showViewAll = true,
  viewAllHref,
  className,
  autoRefreshMs = 0,
}: UserActivityTimelineProps) {
  const [activities, setActivities] = useState<UserActivityDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filters, setFilters] = useState<UserActivityFilters>({ limit: maxEvents });
  const [showFiltersPanel, setShowFiltersPanel] = useState(false);

  const fetchActivities = useCallback(async () => {
    try {
      setError(null);
      const result = userId
        ? await api.admin.getUserActivity(userId, filters)
        : await api.auth.getMyActivity(filters);

      setActivities(result.activities);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to load activity';
      setError(message);
      console.error('Failed to fetch user activity:', err);
    } finally {
      setIsLoading(false);
    }
  }, [userId, filters]);

  useEffect(() => {
    fetchActivities();
  }, [fetchActivities]);

  // Auto-refresh
  useEffect(() => {
    if (autoRefreshMs > 0) {
      const interval = setInterval(fetchActivities, autoRefreshMs);
      return () => clearInterval(interval);
    }
  }, [autoRefreshMs, fetchActivities]);

  const handleRefresh = () => {
    setIsLoading(true);
    fetchActivities();
  };

  const handleApplyFilters = (newFilters: Partial<UserActivityFilters>) => {
    setFilters(prev => ({ ...prev, ...newFilters, limit: maxEvents }));
    setIsLoading(true);
  };

  const handleResetFilters = () => {
    setFilters({ limit: maxEvents });
    setIsLoading(true);
  };

  const events = activities.map(mapActivityToEvent);

  if (isLoading && activities.length === 0) {
    return (
      <Card className={className}>
        <CardContent className="p-8 flex justify-center items-center">
          <Loader2 className="h-8 w-8 animate-spin text-blue-600" />
          <span className="ml-3 text-gray-600">Caricamento attività...</span>
        </CardContent>
      </Card>
    );
  }

  return (
    <div className={className}>
      {/* Filters Panel */}
      {showFilters && (
        <Card className="mb-4">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-base font-semibold">Filtri</CardTitle>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setShowFiltersPanel(!showFiltersPanel)}
              className="flex items-center gap-2"
            >
              {showFiltersPanel ? <X className="h-4 w-4" /> : <Filter className="h-4 w-4" />}
              {showFiltersPanel ? 'Nascondi' : 'Mostra'}
            </Button>
          </CardHeader>

          {showFiltersPanel && (
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {/* Action Filter */}
                <div>
                  <Label htmlFor="action-filter">Tipo Azione</Label>
                  <Select
                    value={filters.actionFilter || 'all'}
                    onValueChange={value =>
                      handleApplyFilters({ actionFilter: value === 'all' ? undefined : value })
                    }
                  >
                    <SelectTrigger id="action-filter">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {ACTION_FILTER_OPTIONS.map(opt => (
                        <SelectItem key={opt.value} value={opt.value}>
                          {opt.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                {/* Resource Filter */}
                <div>
                  <Label htmlFor="resource-filter">Risorsa</Label>
                  <Select
                    value={filters.resourceFilter || 'all'}
                    onValueChange={value =>
                      handleApplyFilters({ resourceFilter: value === 'all' ? undefined : value })
                    }
                  >
                    <SelectTrigger id="resource-filter">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {RESOURCE_FILTER_OPTIONS.map(opt => (
                        <SelectItem key={opt.value} value={opt.value}>
                          {opt.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                {/* Date Range */}
                <div>
                  <Label htmlFor="start-date">Data Inizio</Label>
                  <Input
                    id="start-date"
                    type="date"
                    value={filters.startDate ? filters.startDate.toISOString().split('T')[0] : ''}
                    onChange={e =>
                      handleApplyFilters({
                        startDate: e.target.value ? new Date(e.target.value) : undefined,
                      })
                    }
                  />
                </div>

                <div>
                  <Label htmlFor="end-date">Data Fine</Label>
                  <Input
                    id="end-date"
                    type="date"
                    value={filters.endDate ? filters.endDate.toISOString().split('T')[0] : ''}
                    onChange={e =>
                      handleApplyFilters({
                        endDate: e.target.value ? new Date(e.target.value) : undefined,
                      })
                    }
                  />
                </div>
              </div>

              <div className="flex gap-2">
                <Button variant="outline" size="sm" onClick={handleResetFilters}>
                  Reset Filtri
                </Button>
                <Button variant="ghost" size="sm" onClick={handleRefresh} disabled={isLoading}>
                  {isLoading ? (
                    <Loader2 className="h-4 w-4 animate-spin mr-2" />
                  ) : (
                    <RefreshCw className="h-4 w-4 mr-2" />
                  )}
                  Aggiorna
                </Button>
              </div>
            </CardContent>
          )}
        </Card>
      )}

      {/* Error Alert */}
      {error && (
        <Alert variant="destructive" className="mb-4">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {/* Activity Feed */}
      <ActivityFeed
        events={events}
        maxEvents={maxEvents}
        viewAllHref={viewAllHref}
        showViewAll={showViewAll}
      />
    </div>
  );
}
