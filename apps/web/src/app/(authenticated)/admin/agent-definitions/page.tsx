'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus } from 'lucide-react';
import Link from 'next/link';
import { useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { agentDefinitionsApi } from '@/lib/api/agent-definitions.api';
import { BuilderFilters } from '@/components/admin/agent-definitions/BuilderFilters';
import { BuilderTable } from '@/components/admin/agent-definitions/BuilderTable';

export default function AgentDefinitionsPage() {
  const queryClient = useQueryClient();
  const [filters, setFilters] = useState({ activeOnly: false, search: '' });

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

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Agent Definitions</h1>
          <p className="text-muted-foreground">Manage AI agent templates for the AI Lab</p>
        </div>
        <Button asChild>
          <Link href="/admin/agent-definitions/create">
            <Plus className="h-4 w-4 mr-2" />
            Create Agent
          </Link>
        </Button>
      </div>

      <BuilderFilters onFilterChange={setFilters} />

      {isLoading ? (
        <div className="text-center py-12">Loading...</div>
      ) : (
        <BuilderTable data={agents} onDelete={(id) => deleteMutation.mutate(id)} />
      )}
    </div>
  );
}
