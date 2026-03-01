'use client';

import React from 'react';

import { Settings } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Switch } from '@/components/ui/forms/switch';

interface WidgetCardProps {
  title: string;
  icon: React.ReactNode;
  isEnabled: boolean;
  onToggle?: (enabled: boolean) => void;
  onConfigure?: () => void;
  children: React.ReactNode;
  className?: string;
  'data-testid'?: string;
}

/**
 * Reusable widget card wrapper used by all 6 toolkit widgets.
 * Provides consistent header with enable/disable toggle and configure button.
 * Issue #5128 — Epic B.
 */
export function WidgetCard({
  title,
  icon,
  isEnabled,
  onToggle,
  onConfigure,
  children,
  className = '',
  'data-testid': testId,
}: WidgetCardProps) {
  return (
    <Card
      className={`flex flex-col ${isEnabled ? '' : 'opacity-60'} ${className}`}
      data-testid={testId}
    >
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <CardTitle className="flex items-center gap-2 text-sm font-medium">
          {icon}
          {title}
        </CardTitle>
        <div className="flex items-center gap-2">
          {onConfigure && (
            <Button
              variant="ghost"
              size="icon"
              className="h-7 w-7"
              onClick={onConfigure}
              aria-label={`Configure ${title}`}
            >
              <Settings className="h-4 w-4" />
            </Button>
          )}
          {onToggle && (
            <Switch checked={isEnabled} onCheckedChange={onToggle} aria-label={`Toggle ${title}`} />
          )}
        </div>
      </CardHeader>
      <CardContent className={`flex-1 ${!isEnabled ? 'pointer-events-none select-none' : ''}`}>
        {children}
      </CardContent>
    </Card>
  );
}
