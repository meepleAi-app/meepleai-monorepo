/**
 * Create Typology Page - Issue #3180
 *
 * Page for creating a new AI agent typology.
 * Route: /admin/agent-typologies/create
 */

'use client';

import { useRouter } from 'next/navigation';

import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';
import { TypologyForm } from '@/components/admin/agent-typologies/TypologyForm';
import { useAuthUser } from '@/components/auth/AuthProvider';

function CreateTypologyClient() {
  const router = useRouter();
  const { user, loading: authLoading } = useAuthUser();

  const handleSuccess = () => {
    // Redirect to list page after successful creation
    router.push('/admin/agent-typologies');
  };

  const handleCancel = () => {
    router.back();
  };

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto p-6 max-w-5xl">
        {/* Header */}
        <div className="mb-6">
          <h1 className="text-3xl font-bold">Crea Nuova Tipologia</h1>
          <p className="text-muted-foreground mt-1">
            Definisci una nuova tipologia di agente AI con prompt template e strategia RAG
          </p>
        </div>

        {/* Form Container */}
        <TypologyForm typology={null} onSubmit={handleSuccess} onCancel={handleCancel} />
      </div>
    </AdminAuthGuard>
  );
}

export default function CreateTypologyPage() {
  return <CreateTypologyClient />;
}
