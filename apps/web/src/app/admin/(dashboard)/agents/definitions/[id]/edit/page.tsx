'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { AgentBuilderForm } from '@/components/admin/agent-definitions/AgentBuilderForm';
import { agentDefinitionsApi } from '@/lib/api/agent-definitions.api';
import type { CreateAgentDefinition } from '@/lib/api/schemas/agent-definitions.schemas';

export default function EditAgentDefinitionPage({ params }: { params: { id: string } }) {
  const router = useRouter();
  const queryClient = useQueryClient();

  const { data: agent, isLoading } = useQuery({
    queryKey: ['admin', 'agent-definitions', params.id],
    queryFn: () => agentDefinitionsApi.getById(params.id),
  });

  const updateMutation = useMutation({
    mutationFn: (data: CreateAgentDefinition) => agentDefinitionsApi.update(params.id, data),
    onSuccess: result => {
      toast.success(`Agent "${result.name}" updated successfully`);
      queryClient.invalidateQueries({ queryKey: ['admin', 'agent-definitions'] });
      router.push('/admin/agents/definitions');
    },
    onError: (error: Error) => {
      toast.error(`Failed to update agent: ${error.message}`);
    },
  });

  if (isLoading) {
    return <div className="text-center py-12">Loading...</div>;
  }

  if (!agent) {
    return <div className="text-center py-12">Agent not found</div>;
  }

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Edit Agent Definition</h1>
        <p className="text-muted-foreground">Update {agent.name} configuration</p>
      </div>

      <div className="bg-card p-6 rounded-lg border">
        <AgentBuilderForm
          defaultValues={{
            name: agent.name,
            description: agent.description,
            model: agent.config.model,
            maxTokens: agent.config.maxTokens,
            temperature: agent.config.temperature,
            prompts: agent.prompts,
            tools: agent.tools,
          }}
          onSubmit={data => updateMutation.mutate(data)}
          isLoading={updateMutation.isPending}
        />
      </div>
    </div>
  );
}
