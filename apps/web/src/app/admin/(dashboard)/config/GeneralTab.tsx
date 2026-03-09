'use client';

import { Settings } from 'lucide-react';

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
      <Card className="bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 font-quicksand">
            <Settings className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
            General Settings
          </CardTitle>
          <CardDescription>
            Core system configuration including environment, locale, and platform defaults.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="rounded-lg border border-dashed border-border/60 p-12 text-center">
            <p className="text-sm font-medium text-foreground">Coming Soon</p>
            <p className="mt-2 text-xs text-muted-foreground">
              General settings configuration will be available in a future update.
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
