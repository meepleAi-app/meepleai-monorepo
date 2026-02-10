'use client';

import { useMutation } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { StrategyEditor } from '@/components/admin/strategies/StrategyEditor';
import type { CreateStrategy } from '@/lib/api/schemas/strategies.schemas';
import { strategiesApi } from '@/lib/api/strategies.api';

export default function CreateStrategyPage() {
  const router = useRouter();

  const createMutation = useMutation({
    mutationFn: (data: CreateStrategy) => strategiesApi.create(data),
    onSuccess: (result) => {
      toast.success(`Strategy "${result.name}" created`);
      router.push('/admin/strategies');
    },
    onError: (error: Error) => {
      toast.error(`Failed: ${error.message}`);
    },
  });

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      <h1 className="text-3xl font-bold">Create RAG Strategy</h1>
      <div className="bg-card p-6 rounded-lg border">
        <StrategyEditor
          onSubmit={(data) => createMutation.mutate(data)}
          isLoading={createMutation.isPending}
        />
      </div>
    </div>
  );
}
