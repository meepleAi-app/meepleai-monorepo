'use client';

import { useState } from 'react';

import { useQueryClient } from '@tanstack/react-query';
import { Pencil } from 'lucide-react';

import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { userKeys } from '@/hooks/queries/useCurrentUser';
import { api } from '@/lib/api';

interface EditProfileSheetProps {
  currentDisplayName: string;
}

export function EditProfileSheet({ currentDisplayName }: EditProfileSheetProps) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [displayName, setDisplayName] = useState(currentDisplayName);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSave() {
    const trimmed = displayName.trim();
    if (!trimmed) {
      setError('Il nome non può essere vuoto.');
      return;
    }
    setIsSaving(true);
    setError(null);
    try {
      await api.auth.updateProfile({ displayName: trimmed });
      await queryClient.invalidateQueries({ queryKey: userKeys.all });
      setOpen(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Errore durante il salvataggio.');
    } finally {
      setIsSaving(false);
    }
  }

  function handleOpenChange(next: boolean) {
    if (!next) {
      setDisplayName(currentDisplayName);
      setError(null);
    }
    setOpen(next);
  }

  return (
    <Sheet open={open} onOpenChange={handleOpenChange}>
      <SheetTrigger asChild>
        <Button variant="ghost" size="sm" className="gap-1 font-nunito">
          <Pencil className="h-3.5 w-3.5" />
          Modifica
        </Button>
      </SheetTrigger>
      <SheetContent>
        <SheetHeader>
          <SheetTitle className="font-quicksand">Modifica profilo</SheetTitle>
          <SheetDescription className="font-nunito">
            Aggiorna il tuo nome visualizzato.
          </SheetDescription>
        </SheetHeader>

        <div className="py-6 space-y-4">
          <div className="space-y-2">
            <Label htmlFor="displayName" className="font-nunito">
              Nome visualizzato
            </Label>
            <Input
              id="displayName"
              value={displayName}
              onChange={e => setDisplayName(e.target.value)}
              placeholder="Il tuo nome"
              maxLength={50}
              disabled={isSaving}
              className="font-nunito"
            />
          </div>
          {error && <p className="text-sm text-destructive font-nunito">{error}</p>}
        </div>

        <SheetFooter>
          <Button
            onClick={handleSave}
            disabled={isSaving || !displayName.trim()}
            className="font-nunito"
          >
            {isSaving ? 'Salvataggio...' : 'Salva'}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}
