'use client';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { CollectionDashboard } from '@/components/collection';

// Create a client for the preview
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: false,
      refetchOnWindowFocus: false,
    },
  },
});

export function CollectionDashboardPreviewClient() {
  return (
    <QueryClientProvider client={queryClient}>
      <div className="min-h-screen bg-background">
        {/* Debug Info Banner */}
        <div className="fixed top-0 left-0 z-50 bg-black/80 text-white px-3 py-1.5 text-xs font-mono rounded-br-lg">
          Preview: <span className="text-teal-400 font-bold">CollectionDashboard</span>
        </div>

        <div className="container mx-auto px-4 py-6 pt-12 max-w-screen-2xl">
          {/* Header */}
          <div className="mb-6">
            <h1 className="text-3xl font-bold tracking-tight font-heading">
              La Mia Collezione
            </h1>
            <p className="text-muted-foreground mt-1">
              Gestisci i tuoi giochi, traccia partite e organizza la collezione
            </p>
          </div>

          {/* Collection Dashboard */}
          <CollectionDashboard />
        </div>
      </div>
    </QueryClientProvider>
  );
}
