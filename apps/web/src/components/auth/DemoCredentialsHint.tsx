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
    description: 'Full system access for testing admin features'
  },
  {
    role: 'Editor',
    email: 'editor@meepleai.dev',
    description: 'Content editing and game management access'
  },
  {
    role: 'User',
    email: 'user@meepleai.dev',
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
  if (variant === 'compact') {
    return (
      <div className="text-sm text-slate-600 dark:text-slate-400 space-y-2">
        <p className="font-medium">Demo Accounts:</p>
        {DEMO_CREDENTIALS.map((cred) => (
          <div key={cred.email} className="flex items-center gap-2">
            <span className="text-xs font-medium">{cred.role}:</span>
            <code className="text-xs bg-slate-100 dark:bg-slate-800 px-2 py-1 rounded">
              {cred.email}
            </code>
          </div>
        ))}
        <p className="text-xs text-slate-500 mt-2">
          Click &quot;Try Demo&quot; for instant passwordless access
        </p>
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
                  <div>
                    <p className="text-xs text-slate-500 dark:text-slate-400 mb-1">Email:</p>
                    <code className="block text-xs bg-slate-100 dark:bg-slate-900 px-2 py-1.5 rounded">
                      {cred.email}
                    </code>
                  </div>
                  <p className="text-xs text-slate-500 dark:text-slate-400 italic">
                    No password required - instant access
                  </p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
