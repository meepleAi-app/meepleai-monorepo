/**
 * ISSUE-3709: Agent Builder - Review Step
 * Step 4: Final review and agent creation
 */

'use client';

import { useState } from 'react';
import { Check, Loader2, Play } from 'lucide-react';

import type { AgentForm } from '@/lib/schemas/agent-definition-schema';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/data-display/badge';
import { Separator } from '@/components/ui/navigation/separator';

interface ReviewStepProps {
  agent: AgentForm;
  onSubmit: () => void;
  onTest?: () => Promise<void>;
  isSubmitting?: boolean;
}

export function ReviewStep({ agent, onSubmit, onTest, isSubmitting }: ReviewStepProps) {
  const [isTesting, setIsTesting] = useState(false);

  const handleTest = async () => {
    if (!onTest) return;
    setIsTesting(true);
    try {
      await onTest();
    } finally {
      setIsTesting(false);
    }
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Agent Configuration Summary</CardTitle>
          <CardDescription>Review your agent setup before creating</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Basic Info */}
          <div>
            <h4 className="text-sm font-medium mb-2">Basic Information</h4>
            <dl className="space-y-2 text-sm">
              <div className="flex justify-between">
                <dt className="text-muted-foreground">Name:</dt>
                <dd className="font-medium">{agent.name || '(not set)'}</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-muted-foreground">Type:</dt>
                <dd>
                  <Badge variant="secondary">{agent.type}</Badge>
                </dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-muted-foreground">Model:</dt>
                <dd className="font-mono text-xs">{agent.model}</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-muted-foreground">Temperature:</dt>
                <dd>{agent.temperature.toFixed(1)}</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-muted-foreground">Max Tokens:</dt>
                <dd>{agent.maxTokens.toLocaleString()}</dd>
              </div>
            </dl>
          </div>

          <Separator />

          {/* Prompts */}
          <div>
            <h4 className="text-sm font-medium mb-2">Prompts</h4>
            <p className="text-sm text-muted-foreground">
              {agent.prompts.length} prompt{agent.prompts.length !== 1 ? 's' : ''} configured
            </p>
            <div className="mt-2 space-y-1">
              {agent.prompts.map((prompt, index) => (
                <div key={index} className="flex items-center gap-2 text-xs">
                  <Badge variant="outline" className="capitalize">
                    {prompt.role}
                  </Badge>
                  <span className="text-muted-foreground truncate">
                    {prompt.content.slice(0, 60)}
                    {prompt.content.length > 60 ? '...' : ''}
                  </span>
                </div>
              ))}
            </div>
          </div>

          <Separator />

          {/* Strategy */}
          <div>
            <h4 className="text-sm font-medium mb-2">Retrieval Strategy</h4>
            <dl className="space-y-2 text-sm">
              <div className="flex justify-between">
                <dt className="text-muted-foreground">Strategy:</dt>
                <dd>
                  <Badge>{agent.strategyName || 'None'}</Badge>
                </dd>
              </div>
              {agent.strategyParameters && (
                <div className="flex justify-between">
                  <dt className="text-muted-foreground">Parameters:</dt>
                  <dd className="font-mono text-xs">
                    {JSON.stringify(agent.strategyParameters)}
                  </dd>
                </div>
              )}
            </dl>
          </div>

          <Separator />

          {/* Tools */}
          <div>
            <h4 className="text-sm font-medium mb-2">Tools</h4>
            <p className="text-sm text-muted-foreground">
              {agent.tools?.length || 0} tool{agent.tools?.length !== 1 ? 's' : ''} enabled
            </p>
            {agent.tools && agent.tools.length > 0 && (
              <div className="mt-2 flex flex-wrap gap-1">
                {agent.tools.map((tool) => (
                  <Badge key={tool.name} variant="secondary">
                    {tool.name}
                  </Badge>
                ))}
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Actions */}
      <div className="flex justify-between">
        {onTest && (
          <Button
            type="button"
            variant="outline"
            onClick={handleTest}
            disabled={isTesting || !agent.name}
          >
            {isTesting ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Testing...
              </>
            ) : (
              <>
                <Play className="mr-2 h-4 w-4" />
                Test Agent
              </>
            )}
          </Button>
        )}

        <Button
          type="button"
          onClick={onSubmit}
          disabled={isSubmitting || !agent.name}
          className="ml-auto"
        >
          {isSubmitting ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              Creating...
            </>
          ) : (
            <>
              <Check className="mr-2 h-4 w-4" />
              Create Agent
            </>
          )}
        </Button>
      </div>
    </div>
  );
}
