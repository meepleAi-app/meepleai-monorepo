'use client';
import React from "react";
import { useEffect, useState } from "react";
import { Clock } from "lucide-react";
import { Badge } from "@/components/ui/data-display/badge";

/**
 * Review Lock Timer Component
 *
 * Displays countdown timer with expiring warning for admin review locks.
 *
 * Features:
 * - Precise second-by-second countdown
 * - Visual warning when < 5 minutes remaining
 * - Auto-refresh every second
 * - onExpired callback for lock expiration handling
 *
 * Issue #2748: Frontend - Admin Review Lock UI
 */

export interface ReviewLockTimerProps {
  /** ISO 8601 datetime string when lock expires */
  expiresAt: string;
  /** Callback fired when timer reaches 0 */
  onExpired?: () => void;
  /** Additional className for styling */
  className?: string;
}

export function ReviewLockTimer({ expiresAt, onExpired, className }: ReviewLockTimerProps): JSX.Element {
  const [timeRemaining, setTimeRemaining] = useState(0);

  useEffect(() => {
    const updateTimer = () => {
      const remaining = new Date(expiresAt).getTime() - Date.now();
      setTimeRemaining(Math.max(0, remaining));

      if (remaining <= 0 && onExpired) {
        onExpired();
      }
    };

    updateTimer();
    const interval = setInterval(updateTimer, 1000);
    return () => clearInterval(interval);
  }, [expiresAt, onExpired]);

  const minutes = Math.floor(timeRemaining / 60000);
  const seconds = Math.floor((timeRemaining % 60000) / 1000);
  const isExpiringSoon = minutes < 5;

  return (
    <div className={`flex items-center gap-2 text-sm ${className ?? ''}`}>
      <Clock className={`h-4 w-4 ${isExpiringSoon ? 'text-yellow-600' : 'text-blue-600'}`} />
      <span className={isExpiringSoon ? 'text-yellow-700 font-medium' : 'text-muted-foreground'}>
        {minutes}:{seconds.toString().padStart(2, '0')} remaining
      </span>
      {isExpiringSoon && (
        <Badge variant="outline" className="border-yellow-300 bg-yellow-50 text-yellow-700 ml-1">
          Expiring soon
        </Badge>
      )}
    </div>
  );
}