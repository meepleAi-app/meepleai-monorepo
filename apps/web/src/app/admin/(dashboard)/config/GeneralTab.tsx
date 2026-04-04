'use client';

import { UserCogIcon } from 'lucide-react';

import { EmptyFeatureState } from '@/components/admin/EmptyFeatureState';
import { RegistrationModeToggle } from '@/components/admin/settings/RegistrationModeToggle';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';

export function GeneralTab() {
  return (
    <div className="space-y-6">
      {/* Registration Mode */}
      <Card className="bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 font-quicksand">
            <UserCogIcon className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
            Registration Mode
          </CardTitle>
          <CardDescription>
            Control whether new users can register freely or require an invitation or approved
            access request.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <RegistrationModeToggle />
        </CardContent>
      </Card>

      {/* General Settings */}
      <EmptyFeatureState
        title="Impostazioni Generali"
        description="La configurazione delle impostazioni generali non è ancora disponibile."
      />
    </div>
  );
}
