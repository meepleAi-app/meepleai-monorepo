/**
 * Admin Agent View Page (Task #8 - Issue #239)
 *
 * Agent details page with sections:
 * 1. Agent Info (header)
 * 2. Configuration (model, strategy, prompts)
 * 3. Channel Configuration (WebSocket)
 * 4. Chat Section (AdminAgentChat)
 */

'use client';

import { useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArchiveRestore,
  Bot,
  Edit,
  FlaskConical,
  MessageCircle,
  Radio,
  Rocket,
  Settings,
} from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Separator } from '@/components/ui/navigation/separator';
import { Button } from '@/components/ui/primitives/button';
import { agentDefinitionsApi } from '@/lib/api/agent-definitions.api';

// ============================================================================
// Constants
// ============================================================================

const STATUS_LABELS = ['Draft', 'Testing', 'Published'] as const;

function getStatusBadge(status: number) {
  const label = STATUS_LABELS[status] ?? 'Draft';
  switch (status) {
    case 1:
      return (
        <Badge variant="outline" className="border-amber-500 text-amber-600">
          {label}
        </Badge>
      );
    case 2:
      return (
        <Badge variant="default" className="bg-green-600">
          {label}
        </Badge>
      );
    default:
      return <Badge variant="secondary">{label}</Badge>;
  }
}

// ============================================================================
// Main Component
// ============================================================================

export default function AdminAgentViewPage() {
  const params = useParams();
  const agentId = params?.id as string;
  const queryClient = useQueryClient();

  // Channel enabled state (from Channel Config section)
  const [channelEnabled, setChannelEnabled] = useState(false);

  // Fetch agent data
  const { data: agent, isLoading } = useQuery({
    queryKey: ['admin', 'agent-definitions', agentId],
    queryFn: () => agentDefinitionsApi.getById(agentId),
    enabled: !!agentId,
  });

  // Lifecycle mutations
  const startTestingMutation = useMutation({
    mutationFn: (id: string) => agentDefinitionsApi.startTesting(id),
    onSuccess: () => {
      toast.success('Agent moved to Testing');
      queryClient.invalidateQueries({ queryKey: ['admin', 'agent-definitions'] });
    },
    onError: () => {
      toast.error('Failed to start testing');
    },
  });

  const publishMutation = useMutation({
    mutationFn: (id: string) => agentDefinitionsApi.publish(id),
    onSuccess: () => {
      toast.success('Agent published');
      queryClient.invalidateQueries({ queryKey: ['admin', 'agent-definitions'] });
    },
    onError: () => {
      toast.error('Failed to publish agent');
    },
  });

  const unpublishMutation = useMutation({
    mutationFn: (id: string) => agentDefinitionsApi.unpublish(id),
    onSuccess: () => {
      toast.success('Agent unpublished');
      queryClient.invalidateQueries({ queryKey: ['admin', 'agent-definitions'] });
    },
    onError: () => {
      toast.error('Failed to unpublish agent');
    },
  });

  // ============================================================================
  // Loading State
  // ============================================================================

  if (isLoading) {
    return (
      <div className="max-w-7xl mx-auto space-y-6 p-6">
        <Skeleton className="h-12 w-64" />
        <div className="grid lg:grid-cols-2 gap-6">
          <Skeleton className="h-[400px]" />
          <Skeleton className="h-[400px]" />
        </div>
      </div>
    );
  }

  if (!agent) {
    return (
      <div className="max-w-7xl mx-auto p-6">
        <Card>
          <CardContent className="pt-6">
            <p className="text-center text-muted-foreground">Agent not found</p>
          </CardContent>
        </Card>
      </div>
    );
  }

  const status = agent.status ?? 0;
  const isLifecycleLoading =
    startTestingMutation.isPending || publishMutation.isPending || unpublishMutation.isPending;

  // ============================================================================
  // Main Render
  // ============================================================================

  return (
    <div className="max-w-7xl mx-auto space-y-6 p-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <div className="flex h-12 w-12 items-center justify-center rounded-full bg-primary/10">
            <Bot className="h-6 w-6 text-primary" />
          </div>
          <div>
            <h1 className="text-3xl font-bold font-quicksand">{agent.name}</h1>
            <p className="text-muted-foreground font-nunito">{agent.description}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {getStatusBadge(status)}
          <Badge variant={agent.isActive ? 'default' : 'secondary'}>
            {agent.isActive ? 'Active' : 'Inactive'}
          </Badge>

          {/* Lifecycle action buttons */}
          {status === 0 && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => startTestingMutation.mutate(agentId)}
              disabled={isLifecycleLoading}
            >
              <FlaskConical className="h-4 w-4 mr-2" />
              Start Testing
            </Button>
          )}
          {status === 1 && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => publishMutation.mutate(agentId)}
              disabled={isLifecycleLoading}
            >
              <Rocket className="h-4 w-4 mr-2" />
              Publish
            </Button>
          )}
          {status === 2 && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => unpublishMutation.mutate(agentId)}
              disabled={isLifecycleLoading}
            >
              <ArchiveRestore className="h-4 w-4 mr-2" />
              Unpublish
            </Button>
          )}

          <Button asChild variant="outline" size="sm">
            <Link href={`/admin/agents/definitions/${agentId}/edit`}>
              <Edit className="h-4 w-4 mr-2" />
              Edit
            </Link>
          </Button>
        </div>
      </div>

      <Separator />

      {/* Main Content - 2 columns */}
      <div className="grid lg:grid-cols-2 gap-6">
        {/* Left Column */}
        <div className="space-y-6">
          {/* 1. Configuration Section */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 font-quicksand">
                <Settings className="h-5 w-5" />
                Configuration
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div>
                <label className="text-sm font-medium text-muted-foreground">Model</label>
                <p className="text-sm font-mono mt-1">{agent.config.model}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-muted-foreground">Strategy</label>
                <p className="text-sm mt-1">POC Strategy</p>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="text-sm font-medium text-muted-foreground">Temperature</label>
                  <p className="text-sm mt-1">{agent.config.temperature}</p>
                </div>
                <div>
                  <label className="text-sm font-medium text-muted-foreground">Max Tokens</label>
                  <p className="text-sm mt-1">{agent.config.maxTokens}</p>
                </div>
              </div>
              {agent.prompts && agent.prompts.length > 0 && (
                <div>
                  <label className="text-sm font-medium text-muted-foreground">
                    System Prompts
                  </label>
                  <div className="mt-2 space-y-2">
                    {agent.prompts.slice(0, 2).map((prompt, idx) => (
                      <div
                        key={idx}
                        className="text-xs bg-muted p-2 rounded font-mono line-clamp-2"
                      >
                        {prompt.content}
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </CardContent>
          </Card>

          {/* 2. Channel Configuration Section */}
          <Card className="border-blue-200">
            <CardHeader>
              <CardTitle className="flex items-center gap-2 font-quicksand">
                <Radio className="h-5 w-5 text-blue-600" />
                Channel Configuration
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="flex items-center justify-between">
                <label className="text-sm font-medium">Channel Status</label>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setChannelEnabled(!channelEnabled)}
                >
                  {channelEnabled ? 'Disable' : 'Enable'} Channel
                </Button>
              </div>

              {channelEnabled && (
                <div className="space-y-3 pt-3 border-t">
                  <div>
                    <label className="text-sm font-medium text-muted-foreground">
                      WebSocket Endpoint
                    </label>
                    <p className="text-xs font-mono mt-1 bg-muted p-2 rounded">
                      ws://localhost:8080/channel/{agentId}
                    </p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-muted-foreground">Auth Key</label>
                    <p className="text-xs text-muted-foreground mt-1">
                      Session-based (not persisted)
                    </p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-muted-foreground">Status</label>
                    <Badge variant="default" className="ml-2">
                      Connected
                    </Badge>
                  </div>
                </div>
              )}

              {!channelEnabled && (
                <p className="text-sm text-muted-foreground">
                  Enable channel to use WebSocket features
                </p>
              )}
            </CardContent>
          </Card>
        </div>

        {/* Right Column */}
        <div className="space-y-6">
          {/* 3. Chat Section - Redirects to unified chat */}
          <Card className="border-amber-200">
            <CardHeader>
              <CardTitle className="flex items-center gap-2 font-quicksand">
                <MessageCircle className="h-5 w-5 text-amber-600" />
                Agent Chat
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="flex flex-col items-center justify-center py-8 text-center">
                <MessageCircle className="h-10 w-10 text-amber-500/50 mb-3" />
                <p className="text-sm text-muted-foreground mb-4">
                  Agent chat has been migrated to the unified chat system.
                </p>
                <Button asChild variant="outline" size="sm">
                  <Link href="/chat/new">Open Unified Chat</Link>
                </Button>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
