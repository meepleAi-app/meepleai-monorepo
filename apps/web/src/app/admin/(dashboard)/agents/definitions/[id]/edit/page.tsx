'use client';

import { use } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { AgentBuilderForm } from '@/components/admin/agent-definitions/AgentBuilderForm';
import { agentDefinitionsApi } from '@/lib/api/agent-definitions.api';
import type { CreateAgentDefinition } from '@/lib/api/schemas/agent-definitions.schemas';

export default function EditAgentDefinitionPage({ params }: { params: Promise<{ id: string }> }) {
  // 🔴 #4059 — `params` e' una Promise, e in un Client Component va letta con `use()`.
  //
  // Qui la firma dichiarava `{ params: { id: string } }` e il corpo leggeva `params.id`
  // direttamente. Su una Promise quella proprieta' non esiste: il valore era `undefined`, e
  // `getById(undefined)` lo interpolava nel path. Misurato con un id reale nella URL del
  // browser: `404 /api/v1/admin/agent-definitions/undefined`. La pagina poi mostrava
  // «Agent not found», che e' il messaggio di un id inesistente — cioe' la diagnosi sbagliata.
  //
  // ⚠️ Il tipo era la causa, non solo la conseguenza: TypeScript non poteva accorgersene
  // perche' l'annotazione DICHIARAVA un oggetto sincrono. Con `Promise<{ id: string }>`, un
  // accesso diretto a `params.id` non compila piu'. E' per questo che la correzione tocca la
  // firma e non solo il corpo.
  //
  // Il contratto e' quello dei doc della versione installata (Next 16.3.3,
  // `node_modules/next/dist/docs/01-app/03-api-reference/03-file-conventions/dynamic-routes.md`):
  // `params: Promise<{ slug: string }>` con `const { slug } = use(params)` nel client.
  // Nel repo lo rispettavano gia' 36 pagine dinamiche su 37: questa era l'unica fuori.
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();

  const { data: agent, isLoading } = useQuery({
    queryKey: ['admin', 'agent-definitions', id],
    queryFn: () => agentDefinitionsApi.getById(id),
  });

  const updateMutation = useMutation({
    mutationFn: (data: CreateAgentDefinition) => agentDefinitionsApi.update(id, data),
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
