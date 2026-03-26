/**
 * ClaimGuestGames
 *
 * AgentMemory — Task 25
 *
 * Profile page section for claiming guest identities.
 * Allows users to search for guest names and claim their game history.
 */

'use client';

import { useState } from 'react';

import { CheckCircle, Loader2, Search, UserCheck } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import type { ClaimableGuestDto } from '@/lib/api/clients/agentMemoryClient';
import { useApiClient } from '@/lib/api/context';

// ─── Component ───────────────────────────────────────────────────────────────

export function ClaimGuestGames() {
  const api = useApiClient();
  const [searchQuery, setSearchQuery] = useState('');
  const [guests, setGuests] = useState<ClaimableGuestDto[]>([]);
  const [searching, setSearching] = useState(false);
  const [claimingId, setClaimingId] = useState<string | null>(null);
  const [claimedIds, setClaimedIds] = useState<Set<string>>(new Set());
  const [error, setError] = useState<string | null>(null);

  const handleSearch = async () => {
    const trimmed = searchQuery.trim();
    if (!trimmed) return;

    setSearching(true);
    setError(null);
    try {
      const results = await api.agentMemory.getClaimableGuests(trimmed);
      setGuests(results);
    } catch (_err) {
      setError('Failed to search for guests');
    } finally {
      setSearching(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleSearch();
    }
  };

  const handleClaim = async (playerMemoryId: string) => {
    setClaimingId(playerMemoryId);
    setError(null);
    try {
      await api.agentMemory.claimGuest(playerMemoryId);
      setClaimedIds(prev => new Set(prev).add(playerMemoryId));
    } catch (_err) {
      setError('Failed to claim guest identity');
    } finally {
      setClaimingId(null);
    }
  };

  return (
    <div
      className="rounded-xl border border-white/40 bg-white/70 backdrop-blur-md shadow-sm p-4 space-y-4"
      data-testid="claim-guest-games"
    >
      {/* Header */}
      <div className="flex items-center gap-2">
        <UserCheck className="h-4 w-4 text-amber-600" />
        <h3 className="font-quicksand font-semibold text-sm text-gray-900">Claim Guest Games</h3>
      </div>

      <p className="text-xs text-gray-500 font-nunito">
        Search for your guest name to link past game history to your account.
      </p>

      {/* Search */}
      <div className="flex gap-2">
        <Input
          value={searchQuery}
          onChange={e => setSearchQuery(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Enter guest name..."
          className="flex-1 text-sm font-nunito"
          aria-label="Guest name search"
        />
        <Button
          size="sm"
          variant="outline"
          onClick={handleSearch}
          disabled={searching || !searchQuery.trim()}
          className="gap-1"
        >
          {searching ? (
            <Loader2 className="h-3 w-3 animate-spin" />
          ) : (
            <Search className="h-3 w-3" />
          )}
          Search
        </Button>
      </div>

      {/* Error */}
      {error && (
        <p className="text-sm text-red-600 font-nunito" role="alert">
          {error}
        </p>
      )}

      {/* Results */}
      {guests.length > 0 && (
        <ul className="space-y-2" role="list">
          {guests.map(guest => {
            const isClaimed = claimedIds.has(guest.playerMemoryId);
            const isClaiming = claimingId === guest.playerMemoryId;

            return (
              <li
                key={guest.playerMemoryId}
                className="flex items-center gap-3 rounded-lg bg-white/50 px-3 py-2"
              >
                <div className="flex-1 min-w-0">
                  <span className="font-nunito text-sm font-medium text-gray-900">
                    {guest.guestName}
                  </span>
                  {guest.groupName && (
                    <Badge
                      variant="outline"
                      className="ml-2 bg-purple-50 border-purple-200 text-purple-700 text-xs font-nunito"
                    >
                      {guest.groupName}
                    </Badge>
                  )}
                </div>

                {isClaimed ? (
                  <div className="flex items-center gap-1 text-green-600">
                    <CheckCircle className="h-4 w-4" />
                    <span className="text-xs font-nunito">
                      Claimed! Your history has been linked.
                    </span>
                  </div>
                ) : (
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => handleClaim(guest.playerMemoryId)}
                    disabled={isClaiming}
                    className="gap-1 shrink-0"
                  >
                    {isClaiming ? (
                      <Loader2 className="h-3 w-3 animate-spin" />
                    ) : (
                      <UserCheck className="h-3 w-3" />
                    )}
                    Claim
                  </Button>
                )}
              </li>
            );
          })}
        </ul>
      )}

      {/* Empty search results */}
      {!searching && guests.length === 0 && searchQuery.trim() && (
        <p className="text-sm text-gray-500 font-nunito italic">
          No claimable guests found for &ldquo;{searchQuery.trim()}&rdquo;
        </p>
      )}
    </div>
  );
}
