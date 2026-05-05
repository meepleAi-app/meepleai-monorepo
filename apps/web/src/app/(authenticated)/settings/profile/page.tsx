/**
 * Profile Settings Page (M6 follow-up — Fase 2.3.a)
 *
 * Form di editing per avatar, nome visualizzato ed email.
 * Source of truth: admin-mockups/design_files/settings.jsx ProfilePanel (line 242).
 *
 * API constraints: UpdateProfileRequest supporta solo displayName + email.
 * Bio e username del mockup NON sono supportati dal backend → omessi.
 */

'use client';

import { useEffect, useState } from 'react';

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';

import { AvatarUpload } from '@/components/profile/AvatarUpload';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { userKeys } from '@/hooks/queries/useCurrentUser';
import { useToast } from '@/hooks/useToast';
import { api } from '@/lib/api';
import type { UserProfile } from '@/lib/api/schemas/auth.schemas';

export default function ProfileSettingsPage() {
  const router = useRouter();
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const { data: profile, isLoading } = useQuery({
    queryKey: [...userKeys.current(), 'profile'],
    queryFn: () => api.auth.getProfile(),
    staleTime: 5 * 60 * 1000,
  });

  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    if (profile) {
      setDisplayName(profile.displayName ?? '');
      setEmail(profile.email ?? '');
    }
  }, [profile]);

  const isDirty =
    profile !== null &&
    profile !== undefined &&
    (displayName.trim() !== (profile.displayName ?? '') || email.trim() !== (profile.email ?? ''));

  async function handleAvatarUpload(file: File, previewUrl: string): Promise<void> {
    queryClient.setQueryData([...userKeys.current(), 'profile'], (prev: UserProfile | undefined) =>
      prev ? { ...prev, avatarUrl: previewUrl } : prev
    );
    const result = await api.auth.uploadAvatar(file);
    if (result?.avatarUrl) {
      queryClient.setQueryData(
        [...userKeys.current(), 'profile'],
        (prev: UserProfile | undefined) => (prev ? { ...prev, avatarUrl: result.avatarUrl } : prev)
      );
    }
    await queryClient.invalidateQueries({ queryKey: userKeys.all });
    toast({ title: 'Avatar aggiornato', description: 'La tua foto profilo è stata salvata.' });
  }

  async function handleSave() {
    const trimmedName = displayName.trim();
    const trimmedEmail = email.trim();
    if (!trimmedName) {
      toast({
        title: 'Nome richiesto',
        description: 'Il nome visualizzato non può essere vuoto.',
        variant: 'destructive',
      });
      return;
    }
    setIsSaving(true);
    try {
      await api.auth.updateProfile({
        displayName: trimmedName,
        email: trimmedEmail,
      });
      await queryClient.invalidateQueries({ queryKey: userKeys.all });
      toast({ title: 'Profilo salvato', description: 'Le modifiche sono state applicate.' });
    } catch (err) {
      toast({
        title: 'Errore',
        description: err instanceof Error ? err.message : 'Impossibile salvare le modifiche.',
        variant: 'destructive',
      });
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <div className="min-h-dvh bg-background">
      <div className="mx-auto max-w-2xl px-4 py-8">
        <header className="mb-8">
          <h1 className="text-3xl font-bold tracking-tight text-foreground">Profilo</h1>
          <p className="mt-2 text-sm text-muted-foreground">
            Avatar, nome visualizzato ed email del tuo account.
          </p>
        </header>

        {isLoading ? (
          <div className="rounded-xl border border-border bg-card p-8 text-center text-sm text-muted-foreground">
            Caricamento profilo...
          </div>
        ) : (
          <div className="space-y-8">
            {/* Avatar */}
            <section className="rounded-xl border border-border bg-card p-6">
              <div className="flex items-center gap-4">
                <AvatarUpload
                  currentAvatarUrl={profile?.avatarUrl ?? null}
                  displayName={profile?.displayName ?? profile?.email ?? 'User'}
                  onUpload={handleAvatarUpload}
                  size={80}
                />
                <div className="flex-1">
                  <h2 className="text-base font-semibold text-foreground">Foto profilo</h2>
                  <p className="text-sm text-muted-foreground">
                    Clicca sull&apos;avatar per caricare una nuova immagine (max 5MB).
                  </p>
                </div>
              </div>
            </section>

            {/* Form fields */}
            <section className="space-y-5 rounded-xl border border-border bg-card p-6">
              <div className="space-y-2">
                <Label htmlFor="displayName">Nome visualizzato</Label>
                <Input
                  id="displayName"
                  value={displayName}
                  onChange={e => setDisplayName(e.target.value)}
                  placeholder="Il tuo nome"
                  maxLength={50}
                  disabled={isSaving}
                />
                <p className="text-xs text-muted-foreground">
                  Come appari agli altri giocatori ({displayName.length}/50).
                </p>
              </div>

              <div className="space-y-2">
                <Label htmlFor="email">Email</Label>
                <Input
                  id="email"
                  type="email"
                  value={email}
                  onChange={e => setEmail(e.target.value)}
                  placeholder="tu@esempio.com"
                  disabled={isSaving}
                />
                <p className="text-xs text-muted-foreground">
                  Usata per accesso e notifiche. Modificarla può richiedere verifica.
                </p>
              </div>
            </section>

            {/* Actions */}
            <div className="flex items-center justify-between border-t border-border pt-6">
              <button
                type="button"
                onClick={() => router.push('/settings')}
                className="text-sm text-muted-foreground transition-colors hover:text-foreground"
              >
                ← Impostazioni
              </button>
              <Button onClick={handleSave} disabled={!isDirty || isSaving}>
                {isSaving ? 'Salvataggio...' : 'Salva modifiche'}
              </Button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
