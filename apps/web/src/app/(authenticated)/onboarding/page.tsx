'use client';

import { useEffect, useRef, useState } from 'react';

import { ArrowRight, Camera, Loader2, Lock } from 'lucide-react';
import Image from 'next/image';
import { useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { createApiClient } from '@/lib/api';

export default function OnboardingPage() {
  const router = useRouter();
  const { user, loading: authLoading } = useAuth();
  const api = createApiClient();

  const [displayName, setDisplayName] = useState('');
  const [avatarPreview, setAvatarPreview] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fileInputRef = useRef<HTMLInputElement>(null);

  // Redirect if already onboarded
  useEffect(() => {
    if (!authLoading && user?.onboardingCompleted) {
      router.replace('/');
    }
  }, [authLoading, user, router]);

  // Show spinner while auth is loading
  if (authLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-amber-500" />
      </div>
    );
  }

  // No user or already onboarded — return null (redirect in progress)
  if (!user || user.onboardingCompleted) {
    return null;
  }

  const handleAvatarChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // Create preview URL
    const objectUrl = URL.createObjectURL(file);
    setAvatarPreview(objectUrl);

    // Upload to API (silently ignore failures)
    try {
      await api.auth.uploadAvatar(file);
    } catch {
      // Avatar upload is optional — don't block onboarding
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    const trimmed = displayName.trim();
    if (trimmed.length < 2 || trimmed.length > 50) return;

    setIsSubmitting(true);
    setError(null);

    try {
      await api.auth.updateProfile({ displayName: trimmed });
      await api.auth.completeOnboarding(false);
      router.push('/');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Si è verificato un errore. Riprova.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center px-4">
      <div className="w-full max-w-sm">
        <div className="bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md border border-slate-200/60 dark:border-zinc-700/40 rounded-2xl p-8">
          {/* Heading */}
          <div className="mb-6 text-center">
            <h1 className="font-quicksand text-2xl font-bold text-slate-900 dark:text-white">
              Benvenuto!
            </h1>
            <p className="font-nunito text-sm text-slate-500 dark:text-zinc-400 mt-1">
              Completa il tuo profilo per iniziare
            </p>
          </div>

          <form onSubmit={handleSubmit} className="space-y-5">
            {/* Avatar */}
            <div className="flex justify-center">
              <button
                type="button"
                aria-label="Carica avatar"
                onClick={() => fileInputRef.current?.click()}
                className="w-20 h-20 rounded-full bg-slate-100 dark:bg-zinc-700 border-2 border-dashed border-slate-300 dark:border-zinc-600 flex items-center justify-center overflow-hidden hover:border-amber-400 transition-colors cursor-pointer focus:outline-none focus:ring-2 focus:ring-amber-400"
              >
                {avatarPreview ? (
                  <Image
                    src={avatarPreview}
                    alt="Avatar preview"
                    width={80}
                    height={80}
                    className="w-full h-full object-cover"
                  />
                ) : (
                  <Camera className="h-6 w-6 text-slate-400 dark:text-zinc-500" />
                )}
              </button>
              <input
                ref={fileInputRef}
                type="file"
                accept="image/*"
                className="hidden"
                onChange={handleAvatarChange}
                aria-hidden="true"
              />
            </div>

            {/* Display Name */}
            <div className="space-y-1.5">
              <Label htmlFor="displayName" className="font-nunito text-sm font-medium">
                Display Name
              </Label>
              <Input
                id="displayName"
                type="text"
                value={displayName}
                onChange={e => setDisplayName(e.target.value)}
                placeholder="Il tuo nome"
                minLength={2}
                maxLength={50}
                required
                autoFocus
                disabled={isSubmitting}
                className="rounded-xl"
              />
            </div>

            {/* Email (read-only) */}
            <div className="space-y-1.5">
              <Label htmlFor="email" className="font-nunito text-sm font-medium">
                Email
              </Label>
              <div className="relative">
                <Input
                  id="email"
                  type="email"
                  value={user.email}
                  disabled
                  readOnly
                  className="rounded-xl pr-10 bg-slate-50 dark:bg-zinc-800 text-slate-500 dark:text-zinc-400"
                />
                <Lock
                  className="absolute right-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400 dark:text-zinc-500"
                  aria-hidden="true"
                />
              </div>
            </div>

            {/* Error */}
            {error && (
              <p className="font-nunito text-sm text-red-500 dark:text-red-400" role="alert">
                {error}
              </p>
            )}

            {/* Submit */}
            <Button
              type="submit"
              disabled={isSubmitting || displayName.trim().length < 2}
              className="w-full bg-amber-500 hover:bg-amber-600 text-white rounded-xl py-3 font-quicksand font-semibold"
            >
              {isSubmitting ? (
                <Loader2 className="h-4 w-4 animate-spin mr-2" />
              ) : (
                <ArrowRight className="h-4 w-4 mr-2" />
              )}
              Entra
            </Button>
          </form>
        </div>
      </div>
    </div>
  );
}
