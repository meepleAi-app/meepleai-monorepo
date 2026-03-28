'use client';

import { useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Wrench } from 'lucide-react';
import Link from 'next/link';
import { toast } from 'sonner';

import { BuilderFilters } from '@/components/admin/agent-definitions/BuilderFilters';
import { BuilderTable } from '@/components/admin/agent-definitions/BuilderTable';
import { Button } from '@/components/ui';
import { Sheet, SheetContent, SheetTitle } from '@/components/ui/navigation/sheet';
import { agentDefinitionsApi } from '@/lib/api/agent-definitions.api';

import { BuilderClient } from '../builder/BuilderClient';

export default function AgentDefinitionsPage() {
  const queryClient = useQueryClient();
  const [filters, setFilters] = useState({ activeOnly: false, search: '' });
  const [builderOpen, setBuilderOpen] = useState<boolean>(false);

  const { data: agents = [], isLoading } = useQuery({
    queryKey: ['admin', 'agent-definitions', filters],
    queryFn: () => agentDefinitionsApi.getAll(filters),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => agentDefinitionsApi.delete(id),
    onSuccess: () => {
      toast.success('Agent definition deleted');
      queryClient.invalidateQueries({ queryKey: ['admin', 'agent-definitions'] });
    },
    onError: () => {
      toast.error('Failed to delete agent definition');
    },
  });

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

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Agent Definitions</h1>
          <p className="text-muted-foreground">Manage AI agent templates for the AI Lab</p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" onClick={() => setBuilderOpen(true)}>
            <Wrench className="h-4 w-4 mr-2" />
            Strategy Builder
          </Button>
          <Button asChild>
            <Link href="/admin/agents/definitions/create">
              <Plus className="h-4 w-4 mr-2" />
              Create Agent
            </Link>
          </Button>
        </div>
      </div>

      <BuilderFilters onFilterChange={setFilters} />

      {isLoading ? (
        <div className="text-center py-12">Loading...</div>
      ) : (
        <BuilderTable
          data={agents}
          onDelete={id => deleteMutation.mutate(id)}
          onStartTesting={id => startTestingMutation.mutate(id)}
          onPublish={id => publishMutation.mutate(id)}
          onUnpublish={id => unpublishMutation.mutate(id)}
        />
      )}

      <Sheet open={builderOpen} onOpenChange={setBuilderOpen}>
        <SheetContent side="right" className="w-[800px] sm:max-w-[800px] p-0 overflow-y-auto">
          <SheetTitle className="sr-only">Strategy Builder</SheetTitle>
          <BuilderClient />
        </SheetContent>
      </Sheet>
    </div>
  );
}
