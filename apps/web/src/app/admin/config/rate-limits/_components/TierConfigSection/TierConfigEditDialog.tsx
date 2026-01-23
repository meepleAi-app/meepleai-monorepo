'use client';

import { useState, useEffect } from 'react';
import { type RateLimitConfigDto, type UpdateTierConfigRequest } from '@/types';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Spinner } from '@/components/loading/Spinner';

interface TierConfigEditDialogProps {
  tier: string | null;
  config: RateLimitConfigDto | undefined;
  open: boolean;
  onClose: () => void;
  onSave: (data: UpdateTierConfigRequest) => void;
  isLoading: boolean;
}

export function TierConfigEditDialog({
  tier,
  config,
  open,
  onClose,
  onSave,
  isLoading,
}: TierConfigEditDialogProps) {
  const [formData, setFormData] = useState({
    maxPendingRequests: 5,
    maxRequestsPerMonth: 10,
    cooldownDays: 3,
  });

  const [errors, setErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (config) {
      setFormData({
        maxPendingRequests: config.maxPendingRequests,
        maxRequestsPerMonth: config.maxRequestsPerMonth,
        cooldownDays: Math.floor(config.cooldownAfterRejection.totalSeconds / 86400),
      });
      setErrors({});
    }
  }, [config]);

  const validate = (): boolean => {
    const newErrors: Record<string, string> = {};

    if (formData.maxPendingRequests <= 0) {
      newErrors.maxPendingRequests = 'Must be greater than 0';
    }

    if (formData.maxRequestsPerMonth <= 0) {
      newErrors.maxRequestsPerMonth = 'Must be greater than 0';
    }

    if (formData.cooldownDays < 0) {
      newErrors.cooldownDays = 'Cannot be negative';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSave = () => {
    if (!validate()) return;
    onSave(formData);
  };

  const handleClose = () => {
    if (isLoading) {
      if (!confirm('Save in progress. Are you sure you want to close?')) {
        return;
      }
    }
    onClose();
  };

  return (
    <Dialog open={open} onOpenChange={(open) => !open && handleClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit {tier} Tier Limits</DialogTitle>
          <DialogDescription>
            Changes will affect all {tier?.toLowerCase()} users immediately
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div>
            <label htmlFor="maxPending" className="block text-sm font-medium mb-1">
              Maximum Pending Requests
            </label>
            <Input
              id="maxPending"
              type="number"
              value={formData.maxPendingRequests}
              onChange={(e) =>
                setFormData((prev) => ({
                  ...prev,
                  maxPendingRequests: parseInt(e.target.value),
                }))
              }
              min="1"
              disabled={isLoading}
            />
            {errors.maxPendingRequests && (
              <p className="text-sm text-red-600 mt-1">{errors.maxPendingRequests}</p>
            )}
            <p className="text-xs text-gray-600 mt-1">
              How many requests can be pending review at once
            </p>
          </div>

          <div>
            <label htmlFor="maxMonthly" className="block text-sm font-medium mb-1">
              Maximum Requests Per Month
            </label>
            <Input
              id="maxMonthly"
              type="number"
              value={formData.maxRequestsPerMonth}
              onChange={(e) =>
                setFormData((prev) => ({
                  ...prev,
                  maxRequestsPerMonth: parseInt(e.target.value),
                }))
              }
              min="1"
              disabled={isLoading}
            />
            {errors.maxRequestsPerMonth && (
              <p className="text-sm text-red-600 mt-1">{errors.maxRequestsPerMonth}</p>
            )}
          </div>

          <div>
            <label htmlFor="cooldown" className="block text-sm font-medium mb-1">
              Cooldown After Rejection (days)
            </label>
            <Input
              id="cooldown"
              type="number"
              value={formData.cooldownDays}
              onChange={(e) =>
                setFormData((prev) => ({
                  ...prev,
                  cooldownDays: parseInt(e.target.value),
                }))
              }
              min="0"
              disabled={isLoading}
            />
            {errors.cooldownDays && (
              <p className="text-sm text-red-600 mt-1">{errors.cooldownDays}</p>
            )}
            <p className="text-xs text-gray-600 mt-1">
              Days before user can submit again after a rejection
            </p>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={isLoading}>
            Cancel
          </Button>
          <Button onClick={handleSave} disabled={isLoading}>
            {isLoading && <Spinner className="mr-2" />}
            Save Changes
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
