'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus } from 'lucide-react';
import Link from 'next/link';
import { toast } from 'sonner';
import { Button } from '@/components/ui';
import { strategiesApi } from '@/lib/api/strategies.api';

export default function StrategiesPage() {
  const queryClient = useQueryClient();

  const { data: strategies = [], isLoading } = useQuery({
    queryKey: ['admin', 'strategies'],
    queryFn: () => strategiesApi.getAll(),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => strategiesApi.delete(id),
    onSuccess: () => {
      toast.success('Strategy deleted');
      queryClient.invalidateQueries({ queryKey: ['admin', 'strategies'] });
    },
  });

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">RAG Strategies</h1>
          <p className="text-muted-foreground">Manage retrieval-augmented generation pipelines</p>
        </div>
        <Button asChild>
          <Link href="/admin/strategies/create">
            <Plus className="h-4 w-4 mr-2" />
            Create Strategy
          </Link>
        </Button>
      </div>

      {isLoading ? (
        <div className="text-center py-12">Loading...</div>
      ) : (
        <div className="grid gap-4">
          {strategies.map((strategy) => (
            <div key={strategy.id} className="border p-4 rounded-lg flex justify-between items-start">
              <div>
                <h3 className="font-semibold">{strategy.name}</h3>
                <p className="text-sm text-muted-foreground">{strategy.description}</p>
                <p className="text-xs text-muted-foreground mt-2">
                  {strategy.steps.length} steps
                </p>
              </div>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" asChild>
                  <Link href={`/admin/strategies/${strategy.id}/edit`}>Edit</Link>
                </Button>
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={() => deleteMutation.mutate(strategy.id)}
                >
                  Delete
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
