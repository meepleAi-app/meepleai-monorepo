'use client';

import { create } from 'zustand';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

const MAX_RECENTS = 4;
const STORAGE_KEY = 'meeple-recents';

export interface RecentItem {
  id: string;
  entity: MeepleEntityType;
  title: string;
  href: string;
  visitedAt: number;
}

interface RecentsState {
  items: RecentItem[];
  push: (item: Omit<RecentItem, 'visitedAt'>) => void;
  remove: (id: string) => void;
  clear: () => void;
}

function readStorage(): RecentItem[] {
  if (typeof window === 'undefined') return [];
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as RecentItem[]) : [];
  } catch {
    return [];
  }
}

function writeStorage(items: RecentItem[]): void {
  if (typeof window === 'undefined') return;
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(items));
  } catch {
    /* full or unavailable */
  }
}

export const useRecentsStore = create<RecentsState>((set, get) => ({
  items: readStorage(),

  push: item => {
    const now = Date.now();
    const current = get().items.filter(i => i.id !== item.id);
    const next = [{ ...item, visitedAt: now }, ...current].slice(0, MAX_RECENTS);
    writeStorage(next);
    set({ items: next });
  },

  remove: id => {
    const next = get().items.filter(i => i.id !== id);
    writeStorage(next);
    set({ items: next });
  },

  clear: () => {
    writeStorage([]);
    set({ items: [] });
  },
}));
