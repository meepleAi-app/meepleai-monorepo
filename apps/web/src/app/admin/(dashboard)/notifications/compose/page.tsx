/**
 * Admin Compose Notification Page
 *
 * Allows admins to send manual notifications to users
 * via selected channels (In-App, Email).
 */

'use client';

import { useState } from 'react';

import { useMutation } from '@tanstack/react-query';
import { SendIcon } from 'lucide-react';
import { toast } from 'sonner';

import { ChannelSelector } from '@/components/admin/notifications/ChannelSelector';
import { NotificationPreview } from '@/components/admin/notifications/NotificationPreview';
import {
  RecipientSelector,
  type RecipientSelection,
} from '@/components/admin/notifications/RecipientSelector';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api } from '@/lib/api';
import type { SendManualNotificationRequest } from '@/lib/api/schemas/adminNotifications.schemas';

export default function ComposeNotificationPage() {
  const [title, setTitle] = useState('');
  const [message, setMessage] = useState('');
  const [channels, setChannels] = useState<string[]>(['inapp']);
  const [recipients, setRecipients] = useState<RecipientSelection>({ type: 'all' });

  const sendMutation = useMutation({
    mutationFn: (request: SendManualNotificationRequest) =>
      api.adminNotifications.sendManualNotification(request),
    onSuccess: data => {
      const cappedMsg = data.wasCapped ? ' (capped at 100)' : '';
      toast.success(
        `Notification sent to ${data.dispatched} recipient${data.dispatched !== 1 ? 's' : ''}` +
          (data.skipped > 0 ? ` (${data.skipped} skipped)` : '') +
          cappedMsg
      );
      setTitle('');
      setMessage('');
      setChannels(['inapp']);
      setRecipients({ type: 'all' });
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : 'Failed to send notification');
    },
  });

  function handleSend() {
    if (!title.trim() || !message.trim() || channels.length === 0) {
      toast.error('Please fill in all required fields');
      return;
    }

    const request: SendManualNotificationRequest = {
      title: title.trim(),
      message: message.trim(),
      channels: channels as ('inapp' | 'email')[],
      recipientType: recipients.type,
      recipientRole: recipients.role,
      recipientUserIds: recipients.userIds,
    };

    sendMutation.mutate(request);
  }

  const isValid = title.trim().length > 0 && message.trim().length > 0 && channels.length > 0;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Compose Notification
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Send a manual notification to users via selected channels.
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-5 gap-6">
        {/* Form - 3 columns */}
        <div className="lg:col-span-3 space-y-6">
          {/* Channels */}
          <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
            <CardHeader>
              <CardTitle className="text-base">Channels</CardTitle>
            </CardHeader>
            <CardContent>
              <ChannelSelector selected={channels} onChange={setChannels} />
            </CardContent>
          </Card>

          {/* Recipients */}
          <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
            <CardHeader>
              <CardTitle className="text-base">Recipients</CardTitle>
            </CardHeader>
            <CardContent>
              <RecipientSelector value={recipients} onChange={setRecipients} />
            </CardContent>
          </Card>

          {/* Message */}
          <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
            <CardHeader>
              <CardTitle className="text-base">Message</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div>
                <Label className="text-sm font-medium">Title</Label>
                <Input
                  className="mt-1.5 bg-white/70 dark:bg-zinc-800/70"
                  placeholder="Notification title"
                  value={title}
                  onChange={e => setTitle(e.target.value)}
                  maxLength={200}
                />
                <p className="text-xs text-muted-foreground mt-1">{title.length}/200</p>
              </div>
              <div>
                <Label className="text-sm font-medium">Body</Label>
                <Textarea
                  className="mt-1.5 min-h-[120px] bg-white/70 dark:bg-zinc-800/70"
                  placeholder="Write your notification message..."
                  value={message}
                  onChange={e => setMessage(e.target.value)}
                  maxLength={2000}
                />
                <p className="text-xs text-muted-foreground mt-1">{message.length}/2000</p>
              </div>
            </CardContent>
          </Card>

          {/* Send Button */}
          <Button
            size="lg"
            className="w-full gap-2"
            onClick={handleSend}
            disabled={!isValid || sendMutation.isPending}
          >
            <SendIcon className="h-4 w-4" />
            {sendMutation.isPending ? 'Sending...' : 'Send Notification'}
          </Button>
        </div>

        {/* Preview - 2 columns */}
        <div className="lg:col-span-2">
          <div className="sticky top-4">
            <h3 className="font-quicksand text-sm font-semibold text-muted-foreground mb-3">
              PREVIEW
            </h3>
            <NotificationPreview title={title} message={message} channels={channels} />
          </div>
        </div>
      </div>
    </div>
  );
}
