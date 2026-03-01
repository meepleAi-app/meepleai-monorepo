'use client';

import { useMutation } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { AgentBuilderForm } from '@/components/admin/agent-definitions/AgentBuilderForm';
import { agentDefinitionsApi } from '@/lib/api/agent-definitions.api';
import type { CreateAgentDefinition } from '@/lib/api/schemas/agent-definitions.schemas';

export default function CreateAgentDefinitionPage() {
  const router = useRouter();

  const createMutation = useMutation({
    mutationFn: (data: CreateAgentDefinition) => agentDefinitionsApi.create(data),
    onSuccess: result => {
      toast.success(`Agent "${result.name}" created successfully`);
      router.push('/admin/agents/definitions');
    },
    onError: (error: Error) => {
      toast.error(`Failed to create agent: ${error.message}`);
    },
  });

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Create Agent Definition</h1>
        <p className="text-muted-foreground">Configure a new AI agent template</p>
      </div>

      <div className="bg-card p-6 rounded-lg border">
        <AgentBuilderForm
          onSubmit={data => createMutation.mutate(data)}
          isLoading={createMutation.isPending}
        />
      </div>
    </div>
  );
}
