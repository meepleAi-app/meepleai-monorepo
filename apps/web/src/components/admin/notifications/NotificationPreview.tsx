'use client';

import { BellIcon, MailIcon } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';

interface NotificationPreviewProps {
  title: string;
  message: string;
  channels: string[];
}

export function NotificationPreview({ title, message, channels }: NotificationPreviewProps) {
  if (!title && !message) {
    return (
      <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-dashed">
        <CardContent className="p-6 text-center text-muted-foreground text-sm">
          Start typing to see a preview
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-3">
      {channels.includes('inapp') && (
        <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <BellIcon className="h-4 w-4 text-teal-500" />
              In-App Preview
            </CardTitle>
          </CardHeader>
          <CardContent className="pb-4">
            <div className="border-l-2 border-teal-400 pl-3">
              <p className="font-medium text-sm">{title}</p>
              <p className="text-sm text-muted-foreground mt-1">{message}</p>
            </div>
          </CardContent>
        </Card>
      )}

      {channels.includes('email') && (
        <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <MailIcon className="h-4 w-4 text-blue-500" />
              Email Preview
            </CardTitle>
          </CardHeader>
          <CardContent className="pb-4">
            <div className="bg-slate-50 dark:bg-zinc-900/50 rounded-lg p-3">
              <p className="text-xs text-muted-foreground">Subject:</p>
              <p className="font-medium text-sm">{title}</p>
              <hr className="my-2 border-border/30" />
              <p className="text-sm">{message}</p>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
