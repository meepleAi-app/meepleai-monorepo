'use client';

import { useState } from 'react';
import { X } from 'lucide-react';

/**
 * Modal for changing user tier (Issue #3699)
 * Includes Level 1 confirmation for tier changes
 */

export type Tier = 'free' | 'basic' | 'pro' | 'enterprise';

interface ChangeTierModalProps {
  isOpen: boolean;
  onClose: () => void;
  user: {
    id: string;
    email: string;
    displayName: string;
    tier?: string;
  };
  onConfirm: (userId: string, newTier: Tier) => Promise<void>;
}

const tierDescriptions: Record<Tier, { limit: string; price: string }> = {
  free: { limit: '10,000 tokens/month', price: '€0/month' },
  basic: { limit: '50,000 tokens/month', price: '€9.99/month' },
  pro: { limit: '200,000 tokens/month', price: '€29.99/month' },
  enterprise: { limit: 'Unlimited tokens', price: 'Custom pricing' },
};

export function ChangeTierModal({ isOpen, onClose, user, onConfirm }: ChangeTierModalProps) {
  const [selectedTier, setSelectedTier] = useState<Tier>((user.tier?.toLowerCase() as Tier) || 'free');
  const [showConfirmation, setShowConfirmation] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (!isOpen) return null;

  const currentTier = (user.tier?.toLowerCase() as Tier) || 'free';
  const hasChanged = selectedTier !== currentTier;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!hasChanged) {
      onClose();
      return;
    }

    // Level 1 confirmation: Show confirmation step
    if (!showConfirmation) {
      setShowConfirmation(true);
      return;
    }

    // Confirmed - execute
    try {
      setIsSubmitting(true);
      await onConfirm(user.id, selectedTier);
      onClose();
      setShowConfirmation(false);
    } catch (error) {
      console.error('Failed to change tier:', error);
      setIsSubmitting(false);
    }
  };

  const handleCancel = () => {
    setShowConfirmation(false);
    setSelectedTier(currentTier);
    onClose();
  };

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-xl max-w-md w-full p-6">
        {/* Header */}
        <div className="flex justify-between items-center mb-4">
          <h2 className="text-xl font-semibold">
            {showConfirmation ? 'Confirm Tier Change' : 'Change User Tier'}
          </h2>
          <button
            onClick={handleCancel}
            className="text-gray-400 hover:text-gray-600"
            aria-label="Close"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {!showConfirmation ? (
          /* Step 1: Select Tier */
          <form onSubmit={handleSubmit}>
            <div className="mb-4">
              <p className="text-sm text-gray-600 mb-2">
                User: <strong>{user.displayName}</strong> ({user.email})
              </p>
              <p className="text-sm text-gray-600 mb-4">
                Current Tier: <strong className="capitalize">{currentTier}</strong>
              </p>
            </div>

            <div className="mb-6">
              <label htmlFor="tier-select" className="block text-sm font-medium mb-2">
                Select New Tier
              </label>
              <select
                id="tier-select"
                value={selectedTier}
                onChange={(e) => setSelectedTier(e.target.value as Tier)}
                className="w-full p-2 border border-gray-300 rounded"
              >
                {(['free', 'basic', 'pro', 'enterprise'] as Tier[]).map((tier) => (
                  <option key={tier} value={tier}>
                    {tier.charAt(0).toUpperCase() + tier.slice(1)} - {tierDescriptions[tier].limit} ({tierDescriptions[tier].price})
                  </option>
                ))}
              </select>
            </div>

            <div className="flex gap-3 justify-end">
              <button
                type="button"
                onClick={handleCancel}
                className="px-4 py-2 border border-gray-300 rounded hover:bg-gray-50"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={!hasChanged}
                className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                Continue
              </button>
            </div>
          </form>
        ) : (
          /* Step 2: Level 1 Confirmation */
          <form onSubmit={handleSubmit}>
            <div className="mb-6">
              <div className="bg-orange-50 border border-orange-200 rounded p-4 mb-4">
                <p className="font-medium text-orange-900 mb-2">⚠️ Confirm Tier Change</p>
                <p className="text-sm text-orange-800">
                  You are about to change the tier for <strong>{user.displayName}</strong> from{' '}
                  <strong className="capitalize">{currentTier}</strong> to{' '}
                  <strong className="capitalize">{selectedTier}</strong>.
                </p>
              </div>

              <div className="text-sm text-gray-600">
                <p className="mb-2">This action will:</p>
                <ul className="list-disc list-inside space-y-1">
                  <li>Update token limits immediately</li>
                  <li>Be logged in the audit trail</li>
                  <li>Affect user's monthly quota</li>
                </ul>
              </div>
            </div>

            <div className="flex gap-3 justify-end">
              <button
                type="button"
                onClick={() => setShowConfirmation(false)}
                className="px-4 py-2 border border-gray-300 rounded hover:bg-gray-50"
                disabled={isSubmitting}
              >
                Back
              </button>
              <button
                type="submit"
                disabled={isSubmitting}
                className="px-4 py-2 bg-orange-600 text-white rounded hover:bg-orange-700 disabled:opacity-50"
              >
                {isSubmitting ? 'Updating...' : 'Confirm Change'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
