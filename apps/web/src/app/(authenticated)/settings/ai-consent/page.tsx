/**
 * AI Consent Settings Page (Issue #5512 — GDPR compliance)
 * Manages user consent for AI data processing and external provider usage
 */

'use client';

import React, { useEffect, useState } from 'react';

import { Brain, Globe, Save, Loader2, ShieldCheck, AlertTriangle } from 'lucide-react';

import { Switch } from '@/components/ui/forms/switch';
import { Button } from '@/components/ui/primitives/button';
import { useToast } from '@/hooks/use-toast';

const CURRENT_CONSENT_VERSION = '1.0.0';

interface AiConsentState {
  userId: string;
  consentedToAiProcessing: boolean;
  consentedToExternalProviders: boolean;
  consentedAt: string;
  consentVersion: string;
}

export default function AiConsentPage() {
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
      <div className="container max-w-3xl mx-auto py-8">
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      </div>
    );
  }

  if (!consent) {
    return (
      <div className="container max-w-3xl mx-auto py-8">
        <p className="text-center text-muted-foreground">Failed to load AI consent preferences</p>
      </div>
    );
  }

  return (
    <div className="container max-w-3xl mx-auto py-8">
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold mb-2">AI & Privacy</h1>
          <p className="text-muted-foreground">
            Control how your data is used for AI-powered features
          </p>
        </div>
        <Button
          onClick={handleSave}
          disabled={isSaving || !hasChanges}
          data-testid="save-ai-consent"
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

      {/* GDPR Info Banner */}
      <div className="bg-blue-50 dark:bg-blue-950/20 border border-blue-200 dark:border-blue-800 rounded-xl p-4 mb-6">
        <div className="flex items-start gap-3">
          <ShieldCheck className="h-5 w-5 text-blue-600 dark:text-blue-400 mt-0.5 shrink-0" />
          <div>
            <p className="text-sm font-medium text-blue-900 dark:text-blue-100">
              Your privacy matters
            </p>
            <p className="text-sm text-blue-700 dark:text-blue-300 mt-1">
              Under GDPR, you have the right to control how your data is processed. You can change
              these settings at any time. Disabling AI features will switch to traditional
              search-only mode.
            </p>
          </div>
        </div>
      </div>

      {/* AI Processing Consent */}
      <div className="bg-card rounded-xl p-6 border border-border/50 mb-6">
        <div className="flex items-start gap-4 mb-6">
          <div className="p-2 rounded-lg bg-purple-100 dark:bg-purple-900/20">
            <Brain className="h-6 w-6 text-purple-600 dark:text-purple-400" />
          </div>
          <div className="flex-1">
            <h2 className="text-xl font-bold mb-1">AI-Powered Features</h2>
            <p className="text-sm text-muted-foreground">
              Enable AI to analyze your questions and provide intelligent answers about board game
              rules and strategies
            </p>
          </div>
        </div>

        <div className="flex items-center justify-between py-2">
          <div>
            <p className="font-medium">AI question answering</p>
            <p className="text-sm text-muted-foreground">
              Use AI models to generate answers from game rulebooks and knowledge base
            </p>
          </div>
          <Switch
            checked={consent.consentedToAiProcessing}
            onCheckedChange={checked => updateConsent('consentedToAiProcessing', checked)}
            data-testid="ai-processing-toggle"
          />
        </div>
      </div>

      {/* External Provider Consent */}
      <div className="bg-card rounded-xl p-6 border border-border/50 mb-6">
        <div className="flex items-start gap-4 mb-6">
          <div className="p-2 rounded-lg bg-amber-100 dark:bg-amber-900/20">
            <Globe className="h-6 w-6 text-amber-600 dark:text-amber-400" />
          </div>
          <div className="flex-1">
            <h2 className="text-xl font-bold mb-1">External AI Providers</h2>
            <p className="text-sm text-muted-foreground">
              Some AI features use external providers (OpenRouter) for advanced models. Your prompts
              may be sent to servers outside the EU.
            </p>
          </div>
        </div>

        <div className="flex items-center justify-between py-2">
          <div>
            <p className="font-medium">Allow external AI providers</p>
            <p className="text-sm text-muted-foreground">
              When disabled, only local AI models (Ollama) will be used. This may result in slower
              or less accurate answers.
            </p>
          </div>
          <Switch
            checked={consent.consentedToExternalProviders}
            onCheckedChange={checked => updateConsent('consentedToExternalProviders', checked)}
            disabled={!consent.consentedToAiProcessing}
            data-testid="external-providers-toggle"
          />
        </div>

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
