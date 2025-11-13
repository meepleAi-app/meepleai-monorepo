/**
 * DemoCredentialsHint Component
 *
 * Displays demo credentials for quick testing access.
 * Includes copy-to-clipboard functionality and accessibility features.
 */

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';

// ============================================================================
// Types
// ============================================================================

export interface DemoCredential {
  role: string;
  email: string;
  password: string;
  description: string;
}

export interface DemoCredentialsHintProps {
  onCredentialClick?: (credential: DemoCredential) => void;
  variant?: 'default' | 'compact';
}

// ============================================================================
// Demo Credentials Data
// ============================================================================

const DEMO_CREDENTIALS: DemoCredential[] = [
  {
    role: 'Admin',
    email: 'admin@meepleai.dev',
    password: 'Demo123!',
    description: 'Full system access for testing admin features'
  },
  {
    role: 'Editor',
    email: 'editor@meepleai.dev',
    password: 'Demo123!',
    description: 'Content editing and game management access'
  },
  {
    role: 'User',
    email: 'user@meepleai.dev',
    password: 'Demo123!',
    description: 'Standard user access for testing user features'
  }
];

// ============================================================================
// Component
// ============================================================================

export function DemoCredentialsHint({
  onCredentialClick,
  variant = 'default'
}: DemoCredentialsHintProps) {
  const [copiedEmail, setCopiedEmail] = useState<string | null>(null);

  const handleCopy = async (text: string, email: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setCopiedEmail(email);
      setTimeout(() => setCopiedEmail(null), 2000);
    } catch (err) {
      console.error('Failed to copy to clipboard:', err);
    }
  };

  if (variant === 'compact') {
    return (
      <div className="text-sm text-slate-600 dark:text-slate-400 space-y-2">
        <p className="font-medium">Demo Credentials:</p>
        {DEMO_CREDENTIALS.map((cred) => (
          <div key={cred.email} className="flex items-center gap-2">
            <code className="text-xs bg-slate-100 dark:bg-slate-800 px-2 py-1 rounded">
              {cred.email}
            </code>
            <span className="text-xs text-slate-500">•</span>
            <code className="text-xs bg-slate-100 dark:bg-slate-800 px-2 py-1 rounded">
              {cred.password}
            </code>
          </div>
        ))}
      </div>
    );
  }

  return (
    <Card className="bg-blue-50 dark:bg-blue-950/30 border-blue-200 dark:border-blue-900">
      <CardContent className="pt-4">
        <div className="space-y-3">
          <div className="flex items-start gap-2">
            <span className="text-blue-600 dark:text-blue-400 mt-0.5" aria-hidden="true">
              💡
            </span>
            <div className="flex-1">
              <p className="text-sm font-medium text-blue-900 dark:text-blue-100">
                Quick Test Access
              </p>
              <p className="text-xs text-blue-700 dark:text-blue-300 mt-1">
                Use these demo accounts to explore MeepleAI features:
              </p>
            </div>
          </div>

          <div className="space-y-2">
            {DEMO_CREDENTIALS.map((cred) => (
              <div
                key={cred.email}
                className="bg-white dark:bg-slate-800 rounded-lg p-3 space-y-2"
              >
                <div className="flex items-center justify-between">
                  <span className="text-sm font-medium text-slate-900 dark:text-slate-100">
                    {cred.role}
                  </span>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => onCredentialClick?.(cred)}
                    className="text-xs h-7"
                  >
                    Use this account
                  </Button>
                </div>

                <div className="space-y-1.5">
                  <div className="flex items-center gap-2">
                    <code className="flex-1 text-xs bg-slate-100 dark:bg-slate-900 px-2 py-1.5 rounded">
                      {cred.email}
                    </code>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => handleCopy(cred.email, cred.email)}
                      className="h-7 px-2"
                      aria-label={`Copy ${cred.role} email`}
                    >
                      {copiedEmail === cred.email ? (
                        <span className="text-xs text-green-600">✓</span>
                      ) : (
                        <span className="text-xs">📋</span>
                      )}
                    </Button>
                  </div>

                  <div className="flex items-center gap-2">
                    <code className="flex-1 text-xs bg-slate-100 dark:bg-slate-900 px-2 py-1.5 rounded">
                      {cred.password}
                    </code>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => handleCopy(cred.password, cred.email + '-pwd')}
                      className="h-7 px-2"
                      aria-label={`Copy ${cred.role} password`}
                    >
                      {copiedEmail === cred.email + '-pwd' ? (
                        <span className="text-xs text-green-600">✓</span>
                      ) : (
                        <span className="text-xs">📋</span>
                      )}
                    </Button>
                  </div>
                </div>

                <p className="text-xs text-slate-600 dark:text-slate-400">
                  {cred.description}
                </p>
              </div>
            ))}
          </div>

          <p className="text-xs text-slate-500 dark:text-slate-500 italic">
            Note: Demo accounts are for testing only and reset daily.
          </p>
        </div>
      </CardContent>
    </Card>
  );
}
