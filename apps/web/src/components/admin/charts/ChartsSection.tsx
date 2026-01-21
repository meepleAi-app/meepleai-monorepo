'use client';

import { useQuery } from '@tanstack/react-query';
import { APIRequestsChart, ApiRequestByDay } from './APIRequestsChart';
import { AIUsageDonut, AiUsageStats } from './AIUsageDonut';

export function ChartsSection() {
  // Fetch API requests data
  const { data: apiRequestsData, isLoading: isLoadingRequests } = useQuery<ApiRequestByDay[]>({
    queryKey: ['admin', 'analytics', 'api-requests'],
    queryFn: async () => {
      const response = await fetch('/api/v1/admin/analytics/api-requests?days=7');
      if (!response.ok) {
        throw new Error('Failed to fetch API requests data');
      }
      return response.json();
    },
    refetchInterval: 60000, // Refetch every minute
  });

  // Fetch AI usage data
  const { data: aiUsageData, isLoading: isLoadingUsage } = useQuery<AiUsageStats[]>({
    queryKey: ['admin', 'analytics', 'ai-usage'],
    queryFn: async () => {
      const response = await fetch('/api/v1/admin/analytics/ai-usage');
      if (!response.ok) {
        throw new Error('Failed to fetch AI usage data');
      }
      return response.json();
    },
    refetchInterval: 60000, // Refetch every minute
  });

  return (
    <section className="mb-8">
      <h2 className="mb-4 text-2xl font-bold text-gray-900 dark:text-gray-100">Analytics</h2>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* API Requests Chart */}
        <APIRequestsChart
          data={apiRequestsData ?? []}
          isLoading={isLoadingRequests}
        />

        {/* AI Usage Chart */}
        <AIUsageDonut
          data={aiUsageData ?? []}
          isLoading={isLoadingUsage}
        />
      </div>
    </section>
  );
}
