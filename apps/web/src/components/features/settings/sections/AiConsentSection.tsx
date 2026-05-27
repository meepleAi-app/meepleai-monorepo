/**
 * AiConsentSection — GDPR AI data consent settings (Issue #1608)
 * Migrated from apps/web/src/app/(authenticated)/settings/ai-consent/page.tsx
 */

'use client';

import React, { useEffect, useState } from 'react';

import { Brain, Globe, Save, Loader2, ShieldCheck, AlertTriangle } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { SettingsList } from '@/components/ui/settings-list';
import { SettingsRow } from '@/components/ui/settings-row';
import { ToggleSwitch } from '@/components/ui/toggle-switch';
import { useToast } from '@/hooks/useToast';

const CURRENT_CONSENT_VERSION = '1.0.0';

interface AiConsentState {
  userId: string;
  consentedToAiProcessing: boolean;
  consentedToExternalProviders: boolean;
  consentedAt: string;
  consentVersion: string;
}

export function AiConsentSection(): React.JSX.Element {
  const [consent, setConsent] = useState<AiConsentState | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [hasChanges, setHasChanges] = useState(false);
  const { toast } = useToast();

  useEffect(() => {
    const fetchConsent = async () => {
      try {
        const response = await fetch('/api/v1/users/me/ai-consent');
        if (!response.ok) throw new Error('Failed to fetch AI consent');
        const data = await response.json();
        setConsent(data);
      } catch {
        toast({
          title: 'Error',
          description: 'Failed to load AI consent preferences',
          variant: 'destructive',
        });
      } finally {
        setIsLoading(false);
      }
    };

    void fetchConsent();
  }, [toast]);

  const handleSave = async () => {
    if (!consent) return;

    setIsSaving(true);
    try {
      const response = await fetch('/api/v1/users/me/ai-consent', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          consentedToAiProcessing: consent.consentedToAiProcessing,
          consentedToExternalProviders: consent.consentedToExternalProviders,
          consentVersion: CURRENT_CONSENT_VERSION,
        }),
      });

      if (!response.ok) throw new Error('Failed to save AI consent');

      setHasChanges(false);
      toast({
        title: 'Preferences saved',
        description: 'Your AI consent preferences have been updated',
      });
    } catch {
      toast({
        title: 'Error',
        description: 'Failed to save AI consent preferences',
        variant: 'destructive',
      });
    } finally {
      setIsSaving(false);
    }
  };

  const updateConsent = (
    key: 'consentedToAiProcessing' | 'consentedToExternalProviders',
    value: boolean
  ) => {
    if (!consent) return;

    const updated = { ...consent, [key]: value };

    // If disabling AI processing, also disable external providers
    if (key === 'consentedToAiProcessing' && !value) {
      updated.consentedToExternalProviders = false;
    }

    setConsent(updated);
    setHasChanges(true);
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!consent) {
    return (
      <p className="text-center text-muted-foreground">Failed to load AI consent preferences</p>
    );
  }

  return (
    <div className="space-y-6">
      {/* Section header row */}
      <div className="flex items-center justify-between">
        <h3 className="font-quicksand font-bold text-foreground">AI &amp; data consent</h3>
        <Button
          onClick={handleSave}
          disabled={isSaving || !hasChanges}
          data-testid="save-ai-consent"
          size="sm"
        >
          {isSaving ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              Saving...
            </>
          ) : (
            <>
              <Save className="mr-2 h-4 w-4" />
              Save Changes
            </>
          )}
        </Button>
      </div>

      {/* GDPR Info Banner — blue → chat semantic tokens */}
      <div className="bg-chat/10 border border-chat/30 rounded-xl p-4">
        <div className="flex items-start gap-3">
          <ShieldCheck className="h-5 w-5 text-chat mt-0.5 shrink-0" />
          <div>
            <p className="text-sm font-medium text-foreground">Your privacy matters</p>
            <p className="text-sm text-muted-foreground mt-1">
              Under GDPR, you have the right to control how your data is processed. You can change
              these settings at any time. Disabling AI features will switch to traditional
              search-only mode.
            </p>
          </div>
        </div>
      </div>

      {/* AI Processing Consent */}
      <div>
        <div className="flex items-start gap-4 mb-3">
          {/* purple → entity-player semantic token */}
          <div className="p-2 rounded-lg bg-entity-player/10">
            <Brain className="h-6 w-6 text-entity-player" />
          </div>
          <div className="flex-1">
            <h2 className="text-xl font-bold mb-1">AI-Powered Features</h2>
            <p className="text-sm text-muted-foreground">
              Enable AI to analyze your questions and provide intelligent answers about board game
              rules and strategies
            </p>
          </div>
        </div>

        <SettingsList ariaLabel="AI-Powered Features">
          <SettingsRow
            label="AI question answering"
            description="Use AI models to generate answers from game rulebooks and knowledge base"
            trailing={
              <span data-testid="ai-processing-toggle">
                <ToggleSwitch
                  checked={consent.consentedToAiProcessing}
                  onCheckedChange={checked => updateConsent('consentedToAiProcessing', checked)}
                  ariaLabel="AI question answering"
                  entity="agent"
                />
              </span>
            }
          />
        </SettingsList>
      </div>

      {/* External Provider Consent */}
      <div>
        <div className="flex items-start gap-4 mb-3">
          {/* amber → warning semantic token */}
          <div className="p-2 rounded-lg bg-warning/10">
            <Globe className="h-6 w-6 text-warning" />
          </div>
          <div className="flex-1">
            <h2 className="text-xl font-bold mb-1">External AI Providers</h2>
            <p className="text-sm text-muted-foreground">
              Some AI features use external providers (OpenRouter) for advanced models. Your prompts
              may be sent to servers outside the EU.
            </p>
          </div>
        </div>

        <SettingsList ariaLabel="External AI Providers">
          <SettingsRow
            label="Allow external AI providers"
            description="When disabled, only local AI models (Ollama) will be used. This may result in slower or less accurate answers."
            trailing={
              <span data-testid="external-providers-toggle">
                <ToggleSwitch
                  checked={consent.consentedToExternalProviders}
                  onCheckedChange={checked =>
                    updateConsent('consentedToExternalProviders', checked)
                  }
                  disabled={!consent.consentedToAiProcessing}
                  ariaLabel="Allow external AI providers"
                  entity="agent"
                />
              </span>
            }
          />
        </SettingsList>

        {!consent.consentedToAiProcessing && (
          <div className="flex items-center gap-2 mt-3 text-sm text-muted-foreground">
            <AlertTriangle className="h-4 w-4" />
            <span>Enable AI features above to configure external provider preferences</span>
          </div>
        )}
      </div>

      {/* Data Processing Info */}
      <div className="bg-card rounded-xl p-6 border border-border/50">
        <h3 className="font-bold mb-3">What data is processed</h3>
        <ul className="space-y-2 text-sm text-muted-foreground">
          <li className="flex items-start gap-2">
            <span className="text-primary mt-0.5">&#8226;</span>
            <span>Your questions about board game rules and strategies</span>
          </li>
          <li className="flex items-start gap-2">
            <span className="text-primary mt-0.5">&#8226;</span>
            <span>Conversation history for context (retained for 90 days)</span>
          </li>
          <li className="flex items-start gap-2">
            <span className="text-primary mt-0.5">&#8226;</span>
            <span>
              Request logs for usage tracking (retained for 30 days, pseudonymized after 7 days)
            </span>
          </li>
          <li className="flex items-start gap-2">
            <span className="text-primary mt-0.5">&#8226;</span>
            <span>PII is automatically stripped before sending to external providers</span>
          </li>
        </ul>
        <p className="text-xs text-muted-foreground mt-4">
          Consent version: {CURRENT_CONSENT_VERSION} &middot;{' '}
          {consent.consentedAt && consent.consentedAt !== '0001-01-01T00:00:00'
            ? `Last updated: ${new Date(consent.consentedAt).toLocaleDateString()}`
            : 'Not yet consented'}
        </p>
      </div>
    </div>
  );
}
