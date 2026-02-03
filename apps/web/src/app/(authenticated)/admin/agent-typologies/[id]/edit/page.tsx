/**
 * Edit Typology Page - Issue #3180
 *
 * Page for editing an existing AI agent typology.
 * Route: /admin/agent-typologies/[id]/edit
 */

'use client';

import { use } from 'react';

import { useQuery } from '@tanstack/react-query';
import { AlertTriangle } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';
import { TypologyForm } from '@/components/admin/agent-typologies/TypologyForm';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { agentTypologiesApi } from '@/lib/api/agent-typologies.api';

interface EditTypologyClientProps {
  params: Promise<{ id: string }>;
}

function EditTypologyClient({ params }: EditTypologyClientProps) {
  const { id } = use(params);
  const router = useRouter();
  const { user, loading: authLoading } = useAuthUser();

  // Fetch typology data
  const {
    data: typology,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['typology', id],
    queryFn: () => agentTypologiesApi.getById(id),
    enabled: !!id,
  });

  const handleSuccess = () => {
    // Redirect to list page after successful update
    router.push('/admin/agent-typologies');
  };

  const handleCancel = () => {
    router.back();
  };

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto p-6 max-w-5xl">
        {/* Loading State */}
        {isLoading && (
          <div className="flex items-center justify-center h-64">
            <div className="text-muted-foreground">Caricamento tipologia...</div>
          </div>
        )}

        {/* Error State */}
        {error && (
          <Card className="border-destructive">
            <CardContent className="pt-6">
              <div className="flex items-center gap-2 text-destructive">
                <AlertTriangle className="h-5 w-5" />
                <p>
                  Errore nel caricamento della tipologia:{' '}
                  {error instanceof Error ? error.message : 'Errore sconosciuto'}
                </p>
              </div>
            </CardContent>
          </Card>
        )}

        {/* Form */}
        {!isLoading && !error && typology && (
          <>
            {/* Header */}
            <div className="mb-6">
              <h1 className="text-3xl font-bold">Modifica Tipologia</h1>
              <p className="text-muted-foreground mt-1">
                Aggiorna la configurazione della tipologia &quot;{typology.name}&quot;
              </p>
            </div>

            {/* Form Container */}
            <TypologyForm typology={typology} onSubmit={handleSuccess} onCancel={handleCancel} />
          </>
        )}

        {/* Not Found State */}
        {!isLoading && !error && !typology && (
          <Card className="border-destructive">
            <CardContent className="pt-6">
              <div className="flex items-center gap-2 text-destructive">
                <AlertTriangle className="h-5 w-5" />
                <p>Tipologia non trovata</p>
              </div>
            </CardContent>
          </Card>
        )}
      </div>
    </AdminAuthGuard>
  );
}

export default function EditTypologyPage({ params }: EditTypologyClientProps) {
  return <EditTypologyClient params={params} />;
}
