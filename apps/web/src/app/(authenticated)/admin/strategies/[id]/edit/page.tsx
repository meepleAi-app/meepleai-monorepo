'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { StrategyEditor } from '@/components/admin/strategies/StrategyEditor';
import type { CreateStrategy } from '@/lib/api/schemas/strategies.schemas';
import { strategiesApi } from '@/lib/api/strategies.api';

export default function EditStrategyPage({ params }: { params: { id: string } }) {
  const router = useRouter();
  const queryClient = useQueryClient();

  const { data: strategy, isLoading } = useQuery({
    queryKey: ['admin', 'strategies', params.id],
    queryFn: () => strategiesApi.getById(params.id),
  });

  const updateMutation = useMutation({
    mutationFn: (data: CreateStrategy) => strategiesApi.update(params.id, data),
    onSuccess: (result) => {
      toast.success(`Strategy "${result.name}" updated`);
      queryClient.invalidateQueries({ queryKey: ['admin', 'strategies'] });
      router.push('/admin/strategies');
    },
    onError: (error: Error) => {
      toast.error(`Failed: ${error.message}`);
    },
  });

  if (isLoading) return <div className="text-center py-12">Loading...</div>;
  if (!strategy) return <div className="text-center py-12">Strategy not found</div>;

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      <h1 className="text-3xl font-bold">Edit Strategy</h1>
      <div className="bg-card p-6 rounded-lg border">
        <StrategyEditor
          defaultValues={{
            name: strategy.name,
            description: strategy.description,
            steps: strategy.steps,
          }}
          onSubmit={(data) => updateMutation.mutate(data)}
          isLoading={updateMutation.isPending}
        />
      </div>
    </div>
  );
}
