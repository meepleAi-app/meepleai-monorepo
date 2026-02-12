/**
 * Linked Agent Card Component - Issue #4230
 *
 * Displays information about an AI agent linked to a SharedGame.
 * Shows agent name, model, description, and provides unlink functionality.
 */

'use client';

import { ExternalLink, Trash2 } from 'lucide-react';
import Link from 'next/link';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/overlays/alert-dialog-primitives';
import { Button } from '@/components/ui/primitives/button';
import type { AgentDefinitionDto } from '@/lib/api/schemas/agent-definitions.schemas';

interface LinkedAgentCardProps {
  /**
   * The linked agent definition
   */
  agent: AgentDefinitionDto;

  /**
   * Callback when user confirms unlink
   */
  onUnlink: () => void;

  /**
   * Whether the unlink operation is in progress
   */
  isUnlinking?: boolean;
}

/**
 * LinkedAgentCard component
 *
 * Displays a card with agent information and provides:
 * - Agent name and model
 * - Active/Inactive status badge
 * - Link to agent management page
 * - Unlink button with confirmation dialog
 *
 * @example
 * ```tsx
 * <LinkedAgentCard
 *   agent={agentData}
 *   onUnlink={() => unlinkMutation.mutate()}
 *   isUnlinking={unlinkMutation.isPending}
 * />
 * ```
 */
export function LinkedAgentCard({ agent, onUnlink, isUnlinking }: LinkedAgentCardProps) {
  return (
    <Card>
      <CardHeader>
        <div className="flex items-start justify-between">
          <div>
            <CardTitle>{agent.name}</CardTitle>
            <CardDescription>{agent.description || 'No description provided'}</CardDescription>
          </div>
          <Badge variant={agent.isActive ? 'default' : 'secondary'}>
            {agent.isActive ? 'Active' : 'Inactive'}
          </Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Agent Configuration */}
        <dl className="grid grid-cols-2 gap-2 text-sm">
          <dt className="text-muted-foreground">Model</dt>
          <dd className="font-mono text-xs">{agent.config.model}</dd>

          <dt className="text-muted-foreground">Max Tokens</dt>
          <dd>{agent.config.maxTokens.toLocaleString()}</dd>

          <dt className="text-muted-foreground">Temperature</dt>
          <dd>{agent.config.temperature}</dd>
        </dl>

        {/* Action Buttons */}
        <div className="flex gap-2 pt-2">
          <Link href={`/admin/agent-definitions/${agent.id}`} className="flex-1">
            <Button variant="outline" className="w-full">
              <ExternalLink className="mr-2 h-4 w-4" />
              Manage Agent
            </Button>
          </Link>

          <AlertDialog>
            <AlertDialogTrigger asChild>
              <Button variant="destructive" disabled={isUnlinking}>
                <Trash2 className="mr-2 h-4 w-4" />
                Unlink
              </Button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Unlink Agent?</AlertDialogTitle>
                <AlertDialogDescription>
                  This will remove the link between this game and &quot;{agent.name}&quot;.
                  <br />
                  The agent will not be deleted and can be re-linked later.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction onClick={onUnlink} disabled={isUnlinking}>
                  {isUnlinking ? 'Unlinking...' : 'Unlink Agent'}
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </div>
      </CardContent>
    </Card>
  );
}
