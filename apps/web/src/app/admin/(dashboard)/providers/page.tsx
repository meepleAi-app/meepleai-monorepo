import { ProvidersList } from './ProvidersList';

import type { Metadata } from 'next';

export const metadata: Metadata = { title: 'Providers — Admin' };

export default function ProvidersPage() {
  return (
    <>
      <div>
        <h1 className="font-quicksand text-2xl font-semibold tracking-tight text-foreground">
          LLM Providers
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Stato token e credito residuo per ogni provider configurato.
        </p>
      </div>
      <ProvidersList />
    </>
  );
}
