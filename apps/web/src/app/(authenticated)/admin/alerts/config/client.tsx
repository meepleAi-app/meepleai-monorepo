'use client';

/**
 * Alert Configuration UI (Issue #915)
 *
 * Admin page for configuring alerting system:
 * - Email configuration (SMTP, recipients)
 * - Slack configuration (webhook, channel)
 * - PagerDuty configuration (integration key)
 * - General settings (enabled, throttling)
 * - Test alert functionality
 */

import { useState } from 'react';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Loader2,
  Mail,
  MessageSquare,
  Bell,
  Settings,
  CheckCircle2,
  AlertTriangle,
} from 'lucide-react';

import { AdminAuthGuard } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Switch } from '@/components/ui/forms/switch';
import { Separator } from '@/components/ui/navigation/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { alertConfigApi } from '@/lib/api/alert-config.api';
import { alertRulesApi } from '@/lib/api/alert-rules.api';
import type { UpdateAlertConfiguration } from '@/lib/api/schemas/alert-config.schemas';

type ToastMessage = {
  id: string;
  type: 'success' | 'error' | 'info';
  message: string;
};

/**
 * Email Configuration Form
 */
function EmailConfigForm() {
  const queryClient = useQueryClient();
  const [toastMessages, setToastMessages] = useState<ToastMessage[]>([]);

  const [smtpHost, setSmtpHost] = useState('localhost');
  const [smtpPort, setSmtpPort] = useState('587');
  const [from, setFrom] = useState('noreply@meepleai.dev');
  const [to, setTo] = useState('');
  const [useTls, setUseTls] = useState(true);
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');

  const updateMutation = useMutation({
    mutationFn: async (data: UpdateAlertConfiguration) => {
      return alertConfigApi.update(data);
    },
    onSuccess: () => {
      showToast('success', 'Email configuration updated successfully');
      queryClient.invalidateQueries({ queryKey: ['alertConfig'] });
    },
    onError: (error: Error) => {
      showToast('error', `Failed to update configuration: ${error.message}`);
    },
  });

  const testMutation = useMutation({
    mutationFn: async () => {
      return alertRulesApi.testAlert('test_email', 'email');
    },
    onSuccess: data => {
      if (data.success) {
        showToast('success', 'Test email sent successfully');
      } else {
        showToast('error', 'Test email failed');
      }
    },
    onError: (error: Error) => {
      showToast('error', `Test failed: ${error.message}`);
    },
  });

  function showToast(type: 'success' | 'error' | 'info', message: string) {
    const id = Date.now().toString();
    setToastMessages(prev => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToastMessages(prev => prev.filter(t => t.id !== id));
    }, 5000);
  }

  function handleSave() {
    const config = {
      smtpHost,
      smtpPort: parseInt(smtpPort, 10),
      from,
      to: to
        .split(',')
        .map(e => e.trim())
        .filter(Boolean),
      useTls,
      username: username || undefined,
      password: password || undefined,
    };

    updateMutation.mutate({
      configKey: 'email.smtp',
      configValue: JSON.stringify(config),
      category: 'Email',
      description: 'SMTP email configuration for alerting',
    });
  }

  function handleTest() {
    testMutation.mutate();
  }

  return (
    <div className="space-y-6">
      {toastMessages.map(toast => (
        <Alert key={toast.id} variant={toast.type === 'error' ? 'destructive' : 'default'}>
          {toast.type === 'success' && <CheckCircle2 className="h-4 w-4" />}
          {toast.type === 'error' && <AlertTriangle className="h-4 w-4" />}
          <AlertDescription>{toast.message}</AlertDescription>
        </Alert>
      ))}

      <div className="grid gap-4">
        <div className="grid gap-2">
          <Label htmlFor="smtpHost">SMTP Host</Label>
          <Input
            id="smtpHost"
            value={smtpHost}
            onChange={e => setSmtpHost(e.target.value)}
            placeholder="smtp.gmail.com"
          />
        </div>

        <div className="grid gap-2">
          <Label htmlFor="smtpPort">SMTP Port</Label>
          <Input
            id="smtpPort"
            type="number"
            value={smtpPort}
            onChange={e => setSmtpPort(e.target.value)}
            placeholder="587"
          />
        </div>

        <div className="grid gap-2">
          <Label htmlFor="from">From Address</Label>
          <Input
            id="from"
            type="email"
            value={from}
            onChange={e => setFrom(e.target.value)}
            placeholder="noreply@meepleai.dev"
          />
        </div>

        <div className="grid gap-2">
          <Label htmlFor="to">To Addresses (comma-separated)</Label>
          <Input
            id="to"
            value={to}
            onChange={e => setTo(e.target.value)}
            placeholder="admin@example.com, alerts@example.com"
          />
        </div>

        <div className="flex items-center space-x-2">
          <Switch id="useTls" checked={useTls} onCheckedChange={setUseTls} />
          <Label htmlFor="useTls">Use TLS/SSL</Label>
        </div>

        <Separator />

        <div className="grid gap-2">
          <Label htmlFor="username">SMTP Username (optional)</Label>
          <Input
            id="username"
            value={username}
            onChange={e => setUsername(e.target.value)}
            placeholder="username"
          />
        </div>

        <div className="grid gap-2">
          <Label htmlFor="password">SMTP Password (optional)</Label>
          <Input
            id="password"
            type="password"
            value={password}
            onChange={e => setPassword(e.target.value)}
            placeholder="password"
          />
        </div>
      </div>

      <div className="flex gap-2">
        <Button onClick={handleSave} disabled={updateMutation.isPending}>
          {updateMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          Save Configuration
        </Button>
        <Button variant="outline" onClick={handleTest} disabled={testMutation.isPending}>
          {testMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          Test Email
        </Button>
      </div>
    </div>
  );
}

/**
 * Slack Configuration Form
 */
function SlackConfigForm() {
  const queryClient = useQueryClient();
  const [toastMessages, setToastMessages] = useState<ToastMessage[]>([]);

  const [webhookUrl, setWebhookUrl] = useState('');
  const [channel, setChannel] = useState('#alerts');

  const updateMutation = useMutation({
    mutationFn: async (data: UpdateAlertConfiguration) => {
      return alertConfigApi.update(data);
    },
    onSuccess: () => {
      showToast('success', 'Slack configuration updated successfully');
      queryClient.invalidateQueries({ queryKey: ['alertConfig'] });
    },
    onError: (error: Error) => {
      showToast('error', `Failed to update configuration: ${error.message}`);
    },
  });

  const testMutation = useMutation({
    mutationFn: async () => {
      return alertRulesApi.testAlert('test_slack', 'slack');
    },
    onSuccess: data => {
      if (data.success) {
        showToast('success', 'Test message sent to Slack successfully');
      } else {
        showToast('error', 'Test Slack message failed');
      }
    },
    onError: (error: Error) => {
      showToast('error', `Test failed: ${error.message}`);
    },
  });

  function showToast(type: 'success' | 'error' | 'info', message: string) {
    const id = Date.now().toString();
    setToastMessages(prev => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToastMessages(prev => prev.filter(t => t.id !== id));
    }, 5000);
  }

  function handleSave() {
    const config = {
      webhookUrl,
      channel,
    };

    updateMutation.mutate({
      configKey: 'slack.webhook',
      configValue: JSON.stringify(config),
      category: 'Slack',
      description: 'Slack webhook configuration for alerting',
    });
  }

  function handleTest() {
    testMutation.mutate();
  }

  return (
    <div className="space-y-6">
      {toastMessages.map(toast => (
        <Alert key={toast.id} variant={toast.type === 'error' ? 'destructive' : 'default'}>
          {toast.type === 'success' && <CheckCircle2 className="h-4 w-4" />}
          {toast.type === 'error' && <AlertTriangle className="h-4 w-4" />}
          <AlertDescription>{toast.message}</AlertDescription>
        </Alert>
      ))}

      <div className="grid gap-4">
        <div className="grid gap-2">
          <Label htmlFor="webhookUrl">Webhook URL</Label>
          <Input
            id="webhookUrl"
            type="url"
            value={webhookUrl}
            onChange={e => setWebhookUrl(e.target.value)}
            placeholder="https://hooks.slack.com/services/..."
          />
          <p className="text-sm text-muted-foreground">
            Get your webhook URL from Slack Incoming Webhooks app
          </p>
        </div>

        <div className="grid gap-2">
          <Label htmlFor="channel">Channel</Label>
          <Input
            id="channel"
            value={channel}
            onChange={e => setChannel(e.target.value)}
            placeholder="#alerts"
          />
        </div>
      </div>

      <div className="flex gap-2">
        <Button onClick={handleSave} disabled={updateMutation.isPending}>
          {updateMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          Save Configuration
        </Button>
        <Button variant="outline" onClick={handleTest} disabled={testMutation.isPending}>
          {testMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          Test Slack
        </Button>
      </div>
    </div>
  );
}

/**
 * PagerDuty Configuration Form
 */
function PagerDutyConfigForm() {
  const queryClient = useQueryClient();
  const [toastMessages, setToastMessages] = useState<ToastMessage[]>([]);

  const [integrationKey, setIntegrationKey] = useState('');

  const updateMutation = useMutation({
    mutationFn: async (data: UpdateAlertConfiguration) => {
      return alertConfigApi.update(data);
    },
    onSuccess: () => {
      showToast('success', 'PagerDuty configuration updated successfully');
      queryClient.invalidateQueries({ queryKey: ['alertConfig'] });
    },
    onError: (error: Error) => {
      showToast('error', `Failed to update configuration: ${error.message}`);
    },
  });

  const testMutation = useMutation({
    mutationFn: async () => {
      return alertRulesApi.testAlert('test_pagerduty', 'pagerduty');
    },
    onSuccess: data => {
      if (data.success) {
        showToast('success', 'Test incident created in PagerDuty successfully');
      } else {
        showToast('error', 'Test PagerDuty incident failed');
      }
    },
    onError: (error: Error) => {
      showToast('error', `Test failed: ${error.message}`);
    },
  });

  function showToast(type: 'success' | 'error' | 'info', message: string) {
    const id = Date.now().toString();
    setToastMessages(prev => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToastMessages(prev => prev.filter(t => t.id !== id));
    }, 5000);
  }

  function handleSave() {
    const config = {
      integrationKey,
    };

    updateMutation.mutate({
      configKey: 'pagerduty.integration',
      configValue: JSON.stringify(config),
      category: 'PagerDuty',
      description: 'PagerDuty integration key for alerting',
    });
  }

  function handleTest() {
    testMutation.mutate();
  }

  return (
    <div className="space-y-6">
      {toastMessages.map(toast => (
        <Alert key={toast.id} variant={toast.type === 'error' ? 'destructive' : 'default'}>
          {toast.type === 'success' && <CheckCircle2 className="h-4 w-4" />}
          {toast.type === 'error' && <AlertTriangle className="h-4 w-4" />}
          <AlertDescription>{toast.message}</AlertDescription>
        </Alert>
      ))}

      <div className="grid gap-4">
        <div className="grid gap-2">
          <Label htmlFor="integrationKey">Integration Key</Label>
          <Input
            id="integrationKey"
            type="password"
            value={integrationKey}
            onChange={e => setIntegrationKey(e.target.value)}
            placeholder="Enter PagerDuty integration key"
          />
          <p className="text-sm text-muted-foreground">
            Get your integration key from PagerDuty service configuration
          </p>
        </div>
      </div>

      <div className="flex gap-2">
        <Button onClick={handleSave} disabled={updateMutation.isPending}>
          {updateMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          Save Configuration
        </Button>
        <Button variant="outline" onClick={handleTest} disabled={testMutation.isPending}>
          {testMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          Test PagerDuty
        </Button>
      </div>
    </div>
  );
}

/**
 * General Settings Form
 */
function GeneralSettingsForm() {
  const queryClient = useQueryClient();
  const [toastMessages, setToastMessages] = useState<ToastMessage[]>([]);

  const [enabled, setEnabled] = useState(true);
  const [throttleMinutes, setThrottleMinutes] = useState('60');

  const updateMutation = useMutation({
    mutationFn: async (data: UpdateAlertConfiguration) => {
      return alertConfigApi.update(data);
    },
    onSuccess: () => {
      showToast('success', 'General settings updated successfully');
      queryClient.invalidateQueries({ queryKey: ['alertConfig'] });
    },
    onError: (error: Error) => {
      showToast('error', `Failed to update settings: ${error.message}`);
    },
  });

  function showToast(type: 'success' | 'error' | 'info', message: string) {
    const id = Date.now().toString();
    setToastMessages(prev => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToastMessages(prev => prev.filter(t => t.id !== id));
    }, 5000);
  }

  function handleSave() {
    const config = {
      enabled,
      throttleMinutes: parseInt(throttleMinutes, 10),
    };

    updateMutation.mutate({
      configKey: 'alerting.general',
      configValue: JSON.stringify(config),
      category: 'Global',
      description: 'Global alerting system settings',
    });
  }

  return (
    <div className="space-y-6">
      {toastMessages.map(toast => (
        <Alert key={toast.id} variant={toast.type === 'error' ? 'destructive' : 'default'}>
          {toast.type === 'success' && <CheckCircle2 className="h-4 w-4" />}
          {toast.type === 'error' && <AlertTriangle className="h-4 w-4" />}
          <AlertDescription>{toast.message}</AlertDescription>
        </Alert>
      ))}

      <div className="grid gap-4">
        <div className="flex items-center space-x-2">
          <Switch id="enabled" checked={enabled} onCheckedChange={setEnabled} />
          <Label htmlFor="enabled">Enable Alerting System</Label>
        </div>

        <div className="grid gap-2">
          <Label htmlFor="throttleMinutes">Throttle Window (minutes)</Label>
          <Input
            id="throttleMinutes"
            type="number"
            value={throttleMinutes}
            onChange={e => setThrottleMinutes(e.target.value)}
            placeholder="60"
            min="1"
            max="1440"
          />
          <p className="text-sm text-muted-foreground">
            Minimum time between alerts of the same type (1-1440 minutes)
          </p>
        </div>
      </div>

      <Button onClick={handleSave} disabled={updateMutation.isPending}>
        {updateMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
        Save Settings
      </Button>
    </div>
  );
}

/**
 * Main Alert Configuration Page
 */
export function AlertConfigPageClient() {
  const { user, loading: authLoading } = useAuthUser();

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto py-8 space-y-8">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Alert Configuration</h1>
          <p className="text-muted-foreground mt-2">
            Configure email, Slack, and PagerDuty settings for the alerting system
          </p>
        </div>

        <Tabs defaultValue="email" className="w-full">
          <TabsList className="grid w-full grid-cols-4">
            <TabsTrigger value="email" className="flex items-center gap-2">
              <Mail className="h-4 w-4" />
              Email
            </TabsTrigger>
            <TabsTrigger value="slack" className="flex items-center gap-2">
              <MessageSquare className="h-4 w-4" />
              Slack
            </TabsTrigger>
            <TabsTrigger value="pagerduty" className="flex items-center gap-2">
              <Bell className="h-4 w-4" />
              PagerDuty
            </TabsTrigger>
            <TabsTrigger value="general" className="flex items-center gap-2">
              <Settings className="h-4 w-4" />
              General
            </TabsTrigger>
          </TabsList>

          <TabsContent value="email">
            <Card>
              <CardHeader>
                <CardTitle>Email Configuration</CardTitle>
                <CardDescription>Configure SMTP settings for email alerts</CardDescription>
              </CardHeader>
              <CardContent>
                <EmailConfigForm />
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="slack">
            <Card>
              <CardHeader>
                <CardTitle>Slack Configuration</CardTitle>
                <CardDescription>Configure Slack webhook for channel notifications</CardDescription>
              </CardHeader>
              <CardContent>
                <SlackConfigForm />
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="pagerduty">
            <Card>
              <CardHeader>
                <CardTitle>PagerDuty Configuration</CardTitle>
                <CardDescription>
                  Configure PagerDuty integration for incident management
                </CardDescription>
              </CardHeader>
              <CardContent>
                <PagerDutyConfigForm />
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="general">
            <Card>
              <CardHeader>
                <CardTitle>General Settings</CardTitle>
                <CardDescription>Configure general alerting system settings</CardDescription>
              </CardHeader>
              <CardContent>
                <GeneralSettingsForm />
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>
      </div>
    </AdminAuthGuard>
  );
}
