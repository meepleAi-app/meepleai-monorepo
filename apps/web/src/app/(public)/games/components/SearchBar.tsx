/**
 * SearchBar Component (Issue #1838: PAGE-003)
 *
 * Client component for searching games with debounced input.
 * Updates URL searchParams after 300ms delay to avoid excessive rerenders.
 *
 * Features:
 * - Debounced search (300ms)
 * - URL state persistence (?search=query)
 * - Clear button when query exists
 * - Placeholder text in Italian
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import { Search, X } from 'lucide-react';
import { useRouter, usePathname, useSearchParams } from 'next/navigation';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';

export interface SearchBarProps {
  /** Current search query from URL searchParams */
  currentSearch?: string;
}

export function SearchBar({ currentSearch = '' }: SearchBarProps) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [inputValue, setInputValue] = useState(currentSearch);

  // Update input when URL changes (e.g., browser back/forward)
  useEffect(() => {
    setInputValue(currentSearch);
  }, [currentSearch]);

  // Debounced URL update (300ms)
  useEffect(() => {
    const timer = setTimeout(() => {
      const params = new URLSearchParams(searchParams?.toString() || '');

      if (inputValue) {
        params.set('search', inputValue);
      } else {
        params.delete('search');
      }

      // Reset to page 1 when search changes
      params.delete('page');

      router.push(`${pathname}?${params.toString()}`, { scroll: false });
    }, 300);

    return () => clearTimeout(timer);
  }, [inputValue, router, pathname, searchParams]);

  const handleClear = useCallback(() => {
    setInputValue('');
  }, []);

  return (
    <div className="relative">
      <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
      <Input
        type="search"
        placeholder="Cerca giochi per nome..."
        value={inputValue}
        onChange={e => setInputValue(e.target.value)}
        className="pl-10 pr-10"
        aria-label="Search games"
      />
      {inputValue && (
        <Button
          variant="ghost"
          size="sm"
          onClick={handleClear}
          className="absolute right-1 top-1/2 -translate-y-1/2 h-7 w-7 p-0"
          aria-label="Clear search"
        >
          <X className="h-4 w-4" />
        </Button>
      )}
    </div>
  );
}
