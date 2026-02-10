/**
 * Wishlist API Client - Issue #3920
 *
 * Client for `/api/v1/wishlist/highlights` endpoint
 *
 * @see Issue #3917 - Wishlist Management API (Backend)
 * @see Issue #3920 - Frontend: Wishlist Highlights Component
 */

import type { WishlistHighlightItem } from '@/components/dashboard/WishlistHighlights';

// ============================================================================
// Backend API Response Types
// ============================================================================

interface WishlistItemApiResponse {
  id: string;
  userId: string;
  gameId: string;
  priority: string;
  targetPrice: number | null;
  notes: string | null;
  addedAt: string;
  updatedAt: string | null;
  visibility: string;
}

// ============================================================================
// Mock Data (Temporary - until backend is accessible)
// ============================================================================

const MOCK_HIGHLIGHTS: WishlistHighlightItem[] = [
  {
    id: 'wish-1',
    game: { id: 'game-101', name: 'Terraforming Mars', coverUrl: '' },
    priority: 'HIGH',
    targetPrice: 49.99,
  },
  {
    id: 'wish-2',
    game: { id: 'game-102', name: 'Gloomhaven', coverUrl: '' },
    priority: 'HIGH',
    targetPrice: 89.99,
  },
  {
    id: 'wish-3',
    game: { id: 'game-103', name: 'Brass: Birmingham', coverUrl: '' },
    priority: 'MEDIUM',
  },
  {
    id: 'wish-4',
    game: { id: 'game-104', name: 'Spirit Island', coverUrl: '' },
    priority: 'MEDIUM',
    targetPrice: 59.99,
  },
  {
    id: 'wish-5',
    game: { id: 'game-105', name: 'Root', coverUrl: '' },
    priority: 'LOW',
  },
];

// ============================================================================
// Configuration
// ============================================================================

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

/**
 * Mock data toggle for development
 * Set `NEXT_PUBLIC_USE_MOCK_WISHLIST=false` in `.env.local` to use real API
 */
const USE_MOCK_DATA = process.env.NEXT_PUBLIC_USE_MOCK_WISHLIST !== 'false';

// ============================================================================
// API Client
// ============================================================================

/**
 * Fetch wishlist highlights (top 5 by priority)
 *
 * @returns Promise<WishlistHighlightItem[]>
 */
export async function fetchWishlistHighlights(): Promise<WishlistHighlightItem[]> {
  if (USE_MOCK_DATA) {
    await new Promise((resolve) => setTimeout(resolve, 200));
    return MOCK_HIGHLIGHTS;
  }

  const response = await fetch(
    `${API_BASE_URL}/api/v1/wishlist/highlights`,
    {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
    }
  );

  if (!response.ok) {
    throw new Error(`Wishlist highlights API error: ${response.status} ${response.statusText}`);
  }

  const data: WishlistItemApiResponse[] = await response.json();
  return mapApiResponse(data);
}

// ============================================================================
// Response Mapper
// ============================================================================

function mapApiResponse(data: WishlistItemApiResponse[]): WishlistHighlightItem[] {
  return data.map((item) => ({
    id: item.id,
    game: {
      id: item.gameId,
      name: item.gameId,
      coverUrl: '',
    },
    priority: normalizePriority(item.priority),
    targetPrice: item.targetPrice ?? undefined,
  }));
}

function normalizePriority(priority: string): 'HIGH' | 'MEDIUM' | 'LOW' {
  const upper = priority.toUpperCase();
  if (upper === 'HIGH' || upper === 'MEDIUM' || upper === 'LOW') {
    return upper;
  }
  return 'LOW';
}
