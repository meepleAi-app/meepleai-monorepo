'use client';

import { Suspense } from 'react';

import { useState } from 'react';

import { ActivityFeed, type ActivityEvent } from '@/components/admin/ActivityFeed';
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

// Mock data - replace with API call
const mockEvents: ActivityEvent[] = [
  {
    id: '1',
    eventType: 'user_registered',
    description: 'New user registered: sarah.mitchell@example.com joined the platform',
    timestamp: new Date(Date.now() - 2 * 60 * 1000).toISOString(),
    severity: 'Info',
    userEmail: 'sarah.mitchell@example.com',
    entityType: 'user',
  },
  {
    id: '2',
    eventType: 'game_approved',
    description: 'Game approved: "Wingspan" was added to the catalog',
    timestamp: new Date(Date.now() - 15 * 60 * 1000).toISOString(),
    severity: 'Info',
    entityType: 'game',
  },
  {
    id: '3',
    eventType: 'agent_invoked',
    description: 'AI agent invoked: GameAdvisor agent processed 23 queries',
    timestamp: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
    severity: 'Info',
    entityType: 'agent',
  },
  {
    id: '4',
    eventType: 'document_uploaded',
    description: 'Document uploaded: "Pandemic Legacy Rules.pdf" processed successfully',
    timestamp: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
    severity: 'Info',
    entityType: 'document',
  },
  {
    id: '5',
    eventType: 'user_role_updated',
    description: 'User role updated: john.doe@example.com promoted to Editor',
    timestamp: new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString(),
    severity: 'Info',
    userEmail: 'john.doe@example.com',
    entityType: 'user',
  },
  {
    id: '6',
    eventType: 'config_changed',
    description: 'System configuration changed: RAG strategy updated to HybridRAG',
    timestamp: new Date(Date.now() - 5 * 60 * 60 * 1000).toISOString(),
    severity: 'Warning',
    entityType: 'system',
  },
];

export default function ActivityFeedPage() {
  const [typeFilter, setTypeFilter] = useState<string>('all');
  const [dateRange, setDateRange] = useState<string>('24h');

  const filteredEvents =
    typeFilter === 'all'
      ? mockEvents
      : mockEvents.filter((e) => e.entityType === typeFilter);

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
