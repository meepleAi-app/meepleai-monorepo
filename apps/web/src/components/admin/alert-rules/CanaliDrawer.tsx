'use client';

/**
 * CanaliDrawer (#1840 SP5 F4-C7) — slide-over per gestire config canali alert
 * (Email + Slack). Mockup riga 600-680.
 *
 * Acceptance scenario G:
 *   Given admin click "⚙ Canali"
 *   When drawer apre da destra
 *   Then mostra 2 tab Email | Slack
 *   And form Slack: webhook URL + channel name + test-connection button
 *   And submit → PUT /api/v1/admin/alert-channels/{type}
 *   And Test Connection → POST /test-connection con status pill verde/rosso
 *
 * Strategia masking: la webhookUrl Slack viene mostrata verbatim per
 * consentire al drawer di round-trip senza re-fetch (decisione del subagent
 * BE 1.6 — vedi doc comment AlertChannelsEndpoints.cs). Defense-in-depth
 * masking è tracked come follow-up post-merge.
 */

import { useEffect, useState } from 'react';

import { AlertTriangle, CheckCircle2, Loader2, Save, Send } from 'lucide-react';
import { toast } from 'sonner';

import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/navigation/sheet';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Label } from '@/components/ui/primitives/label';
import { useAlertChannels } from '@/hooks/useAlertChannels';
import type {
  AlertChannel,
  AlertChannelType,
  EmailChannelConfig,
  SlackChannelConfig,
} from '@/lib/api/schemas/alert-channels.schemas';
import {
  emailChannelConfigSchema,
  slackChannelConfigSchema,
} from '@/lib/api/schemas/alert-channels.schemas';
import { cn } from '@/lib/utils';

interface CanaliDrawerProps {
  open: boolean;
  onClose: () => void;
}

function parseConfig<T>(
  json: string,
  schema: { safeParse: (data: unknown) => { success: boolean; data?: T } }
): T | null {
  try {
    const parsed = JSON.parse(json);
    const result = schema.safeParse(parsed);
    return result.success && result.data ? result.data : null;
  } catch {
    return null;
  }
}

function StatusPill({ channel }: { channel: AlertChannel | undefined }) {
  if (!channel || !channel.lastTestStatus) {
    return (
      <span className="inline-flex items-center gap-1 rounded-full border border-border bg-muted/40 px-2 py-0.5 font-mono text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
        Non testato
      </span>
    );
  }
  const isOk = channel.lastTestStatus === 'ok';
  const Icon = isOk ? CheckCircle2 : AlertTriangle;
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full border px-2 py-0.5 font-mono text-[10px] font-bold uppercase tracking-wider',
        isOk
          ? 'border-emerald-400/40 bg-emerald-50 text-emerald-700 dark:border-emerald-500/30 dark:bg-emerald-950/30 dark:text-emerald-300'
          : 'border-rose-400/40 bg-rose-50 text-rose-700 dark:border-rose-500/30 dark:bg-rose-950/30 dark:text-rose-300'
      )}
      title={channel.lastTestMessage ?? undefined}
    >
      <Icon aria-hidden className="h-3 w-3" strokeWidth={2.5} />
      {isOk ? 'connesso' : 'errore'}
    </span>
  );
}

// -----------------------------------------------------------------------------
// Slack tab
// -----------------------------------------------------------------------------

function SlackPanel({
  channel,
  upsert,
  testConnection,
  isUpserting,
  isTesting,
}: {
  channel: AlertChannel | undefined;
  upsert: ReturnType<typeof useAlertChannels>['upsert'];
  testConnection: ReturnType<typeof useAlertChannels>['testConnection'];
  isUpserting: boolean;
  isTesting: boolean;
}) {
  const initial = channel
    ? parseConfig<SlackChannelConfig>(channel.configJson, slackChannelConfigSchema)
    : null;
  const [webhookUrl, setWebhookUrl] = useState(initial?.webhookUrl ?? '');
  const [slackChannel, setSlackChannel] = useState(initial?.channel ?? '');

  // Re-hydrate quando la query React Query rifresca il channel (es. dopo test
  // connection o upsert) e l'oggetto cambia identity.
  useEffect(() => {
    if (channel) {
      const parsed = parseConfig<SlackChannelConfig>(channel.configJson, slackChannelConfigSchema);
      if (parsed) {
        setWebhookUrl(parsed.webhookUrl);
        setSlackChannel(parsed.channel);
      }
    }
  }, [channel]);

  const isValid = webhookUrl.trim().length > 0 && slackChannel.trim().length > 0;
  const canTest = !!channel?.isEnabled && !!initial?.webhookUrl;

  const handleSave = async () => {
    if (!isValid) return;
    try {
      const config: SlackChannelConfig = {
        webhookUrl: webhookUrl.trim(),
        channel: slackChannel.trim(),
      };
      await upsert({
        type: 'slack',
        body: {
          configJson: JSON.stringify(config),
          isEnabled: true,
          rowVersion: channel?.rowVersion ?? null,
        },
      });
      toast.success('Canale Slack salvato');
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore sconosciuto';
      toast.error('Salvataggio Slack fallito', { description: message });
    }
  };

  const handleTest = async () => {
    try {
      const result = await testConnection('slack');
      if (result.success) {
        toast.success('Slack connesso', { description: result.message });
      } else {
        toast.error('Slack non raggiungibile', { description: result.message });
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore sconosciuto';
      toast.error('Test Slack fallito', { description: message });
    }
  };

  return (
    <div className="space-y-4 pt-4">
      <div className="flex items-center justify-between">
        <h4 className="font-quicksand text-sm font-bold text-foreground">Slack webhook</h4>
        <StatusPill channel={channel} />
      </div>

      <div className="space-y-1">
        <Label htmlFor="slack-webhook-url">Webhook URL</Label>
        <input
          id="slack-webhook-url"
          type="url"
          value={webhookUrl}
          onChange={e => setWebhookUrl(e.target.value)}
          placeholder="https://hooks.slack.com/services/T0000/B0000/abcdef..."
          className="w-full rounded border border-border bg-background px-3 py-2 font-mono text-xs text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
          autoComplete="off"
          spellCheck={false}
        />
        <p className="text-[11px] text-muted-foreground">
          Genera un incoming webhook nella tua workspace Slack (Settings → Apps).
        </p>
      </div>

      <div className="space-y-1">
        <Label htmlFor="slack-channel">Canale destinatario</Label>
        <input
          id="slack-channel"
          type="text"
          value={slackChannel}
          onChange={e => setSlackChannel(e.target.value)}
          placeholder="#alerts"
          className="w-full rounded border border-border bg-background px-3 py-2 font-mono text-xs text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
          autoComplete="off"
        />
      </div>

      <div className="flex flex-wrap items-center gap-2 border-t border-border/40 pt-3">
        <button
          type="button"
          onClick={handleSave}
          disabled={!isValid || isUpserting}
          className="inline-flex items-center gap-1.5 rounded bg-primary px-3 py-1.5 text-xs font-semibold text-primary-foreground hover:opacity-90 disabled:opacity-50"
        >
          {isUpserting ? (
            <Loader2 aria-hidden className="h-3.5 w-3.5 animate-spin" />
          ) : (
            <Save aria-hidden className="h-3.5 w-3.5" />
          )}
          Salva
        </button>
        <button
          type="button"
          onClick={handleTest}
          disabled={!canTest || isTesting}
          className="inline-flex items-center gap-1.5 rounded border border-border bg-background px-3 py-1.5 text-xs font-semibold hover:bg-muted disabled:opacity-50"
        >
          {isTesting ? (
            <Loader2 aria-hidden className="h-3.5 w-3.5 animate-spin" />
          ) : (
            <Send aria-hidden className="h-3.5 w-3.5" />
          )}
          Test connection
        </button>
        {channel?.lastTestedAt && (
          <span className="ml-auto font-mono text-[10px] text-muted-foreground">
            ultimo test: {new Date(channel.lastTestedAt).toLocaleString('it-IT')}
          </span>
        )}
      </div>
    </div>
  );
}

// -----------------------------------------------------------------------------
// Email tab
// -----------------------------------------------------------------------------

function EmailPanel({
  channel,
  upsert,
  testConnection,
  isUpserting,
  isTesting,
}: {
  channel: AlertChannel | undefined;
  upsert: ReturnType<typeof useAlertChannels>['upsert'];
  testConnection: ReturnType<typeof useAlertChannels>['testConnection'];
  isUpserting: boolean;
  isTesting: boolean;
}) {
  const initial = channel
    ? parseConfig<EmailChannelConfig>(channel.configJson, emailChannelConfigSchema)
    : null;
  const [smtpHost, setSmtpHost] = useState(initial?.smtpHost ?? '');
  const [smtpPort, setSmtpPort] = useState(initial?.smtpPort?.toString() ?? '587');
  const [fromAddress, setFromAddress] = useState(initial?.fromAddress ?? '');
  const [toAddresses, setToAddresses] = useState((initial?.toAddresses ?? []).join('\n'));

  useEffect(() => {
    if (channel) {
      const parsed = parseConfig<EmailChannelConfig>(channel.configJson, emailChannelConfigSchema);
      if (parsed) {
        setSmtpHost(parsed.smtpHost);
        setSmtpPort(parsed.smtpPort.toString());
        setFromAddress(parsed.fromAddress);
        setToAddresses(parsed.toAddresses.join('\n'));
      }
    }
  }, [channel]);

  const parsedTo = toAddresses
    .split(/\s*[\n,]\s*/)
    .map(a => a.trim())
    .filter(a => a.length > 0);

  const isValid =
    smtpHost.trim().length > 0 &&
    /^\d+$/.test(smtpPort.trim()) &&
    fromAddress.trim().length > 0 &&
    parsedTo.length > 0;

  const handleSave = async () => {
    if (!isValid) return;
    try {
      const config: EmailChannelConfig = {
        smtpHost: smtpHost.trim(),
        smtpPort: parseInt(smtpPort.trim(), 10),
        fromAddress: fromAddress.trim(),
        toAddresses: parsedTo,
      };
      await upsert({
        type: 'email',
        body: {
          configJson: JSON.stringify(config),
          isEnabled: true,
          rowVersion: channel?.rowVersion ?? null,
        },
      });
      toast.success('Canale Email salvato');
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore sconosciuto';
      toast.error('Salvataggio Email fallito', { description: message });
    }
  };

  const handleTest = async () => {
    try {
      const result = await testConnection('email');
      if (result.success) {
        toast.success('SMTP raggiungibile', { description: result.message });
      } else {
        toast.error('SMTP non raggiungibile', { description: result.message });
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore sconosciuto';
      toast.error('Test Email fallito', { description: message });
    }
  };

  return (
    <div className="space-y-4 pt-4">
      <div className="flex items-center justify-between">
        <h4 className="font-quicksand text-sm font-bold text-foreground">SMTP relay</h4>
        <StatusPill channel={channel} />
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <div className="space-y-1 sm:col-span-2">
          <Label htmlFor="email-smtp-host">SMTP host</Label>
          <input
            id="email-smtp-host"
            type="text"
            value={smtpHost}
            onChange={e => setSmtpHost(e.target.value)}
            placeholder="smtp.example.com"
            className="w-full rounded border border-border bg-background px-3 py-2 font-mono text-xs text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            autoComplete="off"
          />
        </div>
        <div className="space-y-1">
          <Label htmlFor="email-smtp-port">Porta</Label>
          <input
            id="email-smtp-port"
            type="number"
            value={smtpPort}
            onChange={e => setSmtpPort(e.target.value)}
            placeholder="587"
            min={1}
            max={65535}
            className="w-full rounded border border-border bg-background px-3 py-2 font-mono text-xs text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
          />
        </div>
      </div>

      <div className="space-y-1">
        <Label htmlFor="email-from">From address</Label>
        <input
          id="email-from"
          type="email"
          value={fromAddress}
          onChange={e => setFromAddress(e.target.value)}
          placeholder="alerts@meepleai.app"
          className="w-full rounded border border-border bg-background px-3 py-2 font-mono text-xs text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
          autoComplete="off"
        />
      </div>

      <div className="space-y-1">
        <Label htmlFor="email-to">Destinatari (uno per riga o separati da virgola)</Label>
        <textarea
          id="email-to"
          value={toAddresses}
          onChange={e => setToAddresses(e.target.value)}
          rows={3}
          placeholder="ops@meepleai.app&#10;oncall@meepleai.app"
          className="w-full rounded border border-border bg-background px-3 py-2 font-mono text-xs text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
        />
        <p className="text-[11px] text-muted-foreground">
          {parsedTo.length} destinatar{parsedTo.length === 1 ? 'io' : 'i'} riconosciut
          {parsedTo.length === 1 ? 'o' : 'i'}.
        </p>
      </div>

      <div className="flex flex-wrap items-center gap-2 border-t border-border/40 pt-3">
        <button
          type="button"
          onClick={handleSave}
          disabled={!isValid || isUpserting}
          className="inline-flex items-center gap-1.5 rounded bg-primary px-3 py-1.5 text-xs font-semibold text-primary-foreground hover:opacity-90 disabled:opacity-50"
        >
          {isUpserting ? (
            <Loader2 aria-hidden className="h-3.5 w-3.5 animate-spin" />
          ) : (
            <Save aria-hidden className="h-3.5 w-3.5" />
          )}
          Salva
        </button>
        <button
          type="button"
          onClick={handleTest}
          disabled={!channel?.isEnabled || isTesting}
          className="inline-flex items-center gap-1.5 rounded border border-border bg-background px-3 py-1.5 text-xs font-semibold hover:bg-muted disabled:opacity-50"
        >
          {isTesting ? (
            <Loader2 aria-hidden className="h-3.5 w-3.5 animate-spin" />
          ) : (
            <Send aria-hidden className="h-3.5 w-3.5" />
          )}
          Test connection
        </button>
        {channel?.lastTestedAt && (
          <span className="ml-auto font-mono text-[10px] text-muted-foreground">
            ultimo test: {new Date(channel.lastTestedAt).toLocaleString('it-IT')}
          </span>
        )}
      </div>
    </div>
  );
}

// -----------------------------------------------------------------------------
// Main drawer
// -----------------------------------------------------------------------------

export function CanaliDrawer({ open, onClose }: CanaliDrawerProps) {
  const [activeTab, setActiveTab] = useState<AlertChannelType>('slack');
  const { channels, upsert, testConnection, isUpserting, isTesting } = useAlertChannels();

  const slackChannel = channels.find(c => c.type === 'slack');
  const emailChannel = channels.find(c => c.type === 'email');

  return (
    <Sheet open={open} onOpenChange={isOpen => !isOpen && onClose()}>
      <SheetContent
        side="right"
        className="flex w-full max-w-md flex-col overflow-y-auto sm:max-w-md"
      >
        <SheetHeader>
          <SheetTitle>Canali notifiche</SheetTitle>
          <SheetDescription>
            Configura Email + Slack che il{' '}
            <code className="font-mono text-[11px]">ChannelDispatchHandler</code> utilizza per le
            notifiche alert (incluso TestAlert in modalità live).
          </SheetDescription>
        </SheetHeader>

        <Tabs
          value={activeTab}
          onValueChange={value => setActiveTab(value as AlertChannelType)}
          className="mt-4 flex flex-1 flex-col"
        >
          <TabsList className="grid w-full grid-cols-2">
            <TabsTrigger value="slack">Slack</TabsTrigger>
            <TabsTrigger value="email">Email</TabsTrigger>
          </TabsList>

          <TabsContent value="slack" className="flex-1">
            <SlackPanel
              channel={slackChannel}
              upsert={upsert}
              testConnection={testConnection}
              isUpserting={isUpserting && activeTab === 'slack'}
              isTesting={isTesting && activeTab === 'slack'}
            />
          </TabsContent>

          <TabsContent value="email" className="flex-1">
            <EmailPanel
              channel={emailChannel}
              upsert={upsert}
              testConnection={testConnection}
              isUpserting={isUpserting && activeTab === 'email'}
              isTesting={isTesting && activeTab === 'email'}
            />
          </TabsContent>
        </Tabs>
      </SheetContent>
    </Sheet>
  );
}
