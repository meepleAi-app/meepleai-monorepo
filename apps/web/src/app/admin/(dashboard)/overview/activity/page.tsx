'use client';

import { Suspense, useEffect } from 'react';

import { useState } from 'react';

import { ActivityFeed, type ActivityEvent } from '@/components/admin/ActivityFeed';
import { adminDashboardClient } from '@/lib/api/clients/adminDashboardClient';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Input } from '@/components/ui/primitives/input';

/**
 * Activity Feed Page
 * Route: /admin/overview/activity
 *
 * Shows timeline of recent admin events (user registrations, game approvals,
 * agent invocations, config changes).
 *
 * Issue: #4628
 */

export default function ActivityFeedPage() {
  const [typeFilter, setTypeFilter] = useState<string>('all');
  const [dateRange, setDateRange] = useState<string>('24h');
  const [events, setEvents] = useState<ActivityEvent[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function fetchActivity() {
      try {
        const data = await adminDashboardClient.getUserActivityLog();
        // Transform API data to ActivityEvent format
        const transformedEvents: ActivityEvent[] = data.activities?.map((a: any) => ({
          id: a.id,
          eventType: a.actionType,
          description: `${a.action}: ${a.target}`,
          timestamp: a.timestamp,
          severity: a.status === 'success' ? 'Info' : 'Warning',
          userEmail: a.userEmail,
          entityType: a.actionType === 'login' ? 'user' : a.actionType === 'approve' ? 'game' : 'system',
        })) || [];
        setEvents(transformedEvents);
      } catch (error) {
        console.error('Failed to fetch activity:', error);
        setEvents([]);
      } finally {
        setLoading(false);
      }
    }
    fetchActivity();
  }, []);

  const filteredEvents =
    typeFilter === 'all'
      ? events
      : events.filter((e) => e.entityType === typeFilter);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-quicksand font-bold text-foreground">
          Activity Feed
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Recent admin events and system activity
        </p>
      </div>

      {/* Filters */}
      <div className="flex gap-4 flex-wrap">
        <Select value={typeFilter} onValueChange={setTypeFilter}>
          <SelectTrigger className="w-48">
            <SelectValue placeholder="Activity type" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Activities</SelectItem>
            <SelectItem value="users">Users</SelectItem>
            <SelectItem value="games">Games</SelectItem>
            <SelectItem value="agents">Agents</SelectItem>
            <SelectItem value="documents">Documents</SelectItem>
            <SelectItem value="system">System</SelectItem>
          </SelectContent>
        </Select>

        <Select value={dateRange} onValueChange={setDateRange}>
          <SelectTrigger className="w-48">
            <SelectValue placeholder="Date range" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="24h">Last 24 Hours</SelectItem>
            <SelectItem value="7d">Last 7 Days</SelectItem>
            <SelectItem value="30d">Last 30 Days</SelectItem>
            <SelectItem value="custom">Custom Range</SelectItem>
          </SelectContent>
        </Select>

        <Input
          type="search"
          placeholder="Search activities..."
          className="w-64"
        />
      </div>

      {/* Activity Timeline */}
      <Suspense
        fallback={
          <div className="space-y-4">
            {[...Array(6)].map((_, i) => (
              <Skeleton key={i} className="h-20 w-full" />
            ))}
          </div>
        }
      >
        <ActivityFeed events={filteredEvents} />
      </Suspense>
    </div>
  );
}
