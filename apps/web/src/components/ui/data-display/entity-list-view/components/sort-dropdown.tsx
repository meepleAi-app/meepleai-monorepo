/**
 * SortDropdown - Dropdown control for sort selection
 *
 * Pattern reused from GameCarousel SortDropdown with generics support.
 * Features click-outside, escape key, and arrow key navigation.
 *
 * @module components/ui/data-display/entity-list-view/components/sort-dropdown
 *
 * @example
 * ```tsx
 * const sortOptions = [
 *   { value: 'rating', label: 'Rating', icon: Star, compareFn: ... },
 *   { value: 'name', label: 'Name', icon: ArrowDownAZ, compareFn: ... },
 * ];
 *
 * <SortDropdown
 *   value="rating"
 *   options={sortOptions}
 *   onChange={(value) => setSort(value)}
 * />
 * ```
 */

'use client';

import React, { useEffect, useRef, useState } from 'react';
import { ChevronDown, Check } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { SortOption } from '../entity-list-view.types';

export interface SortDropdownProps<T> {
  /** Current sort value */
  value: string;
  /** Available sort options */
  options: SortOption<T>[];
  /** Callback when sort changes */
  onChange: (value: string) => void;
  /** Additional CSS classes */
  className?: string;
  /** Test ID */
  'data-testid'?: string;
}

/**
 * SortDropdown component for sort selection
 */
export function SortDropdown<T>({
  value,
  options,
  onChange,
  className,
  'data-testid': testId,
}: SortDropdownProps<T>) {
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  // Close on click outside
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [isOpen]);

  // Close on escape key
  useEffect(() => {
    function handleEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setIsOpen(false);
      }
    }

    if (isOpen) {
      document.addEventListener('keydown', handleEscape);
    }
    return () => document.removeEventListener('keydown', handleEscape);
  }, [isOpen]);

  // Arrow key navigation
  const handleKeyDown = (event: React.KeyboardEvent) => {
    if (!isOpen) return;

    const currentIndex = options.findIndex((opt) => opt.value === value);

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      const nextIndex = (currentIndex + 1) % options.length;
      onChange(options[nextIndex].value);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      const prevIndex = (currentIndex - 1 + options.length) % options.length;
      onChange(options[prevIndex].value);
    } else if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      setIsOpen(false);
    }
  };

  const currentOption = options.find((opt) => opt.value === value) ?? options[0];
  if (!currentOption) return null;

  const CurrentIcon = currentOption.icon;

  return (
    <div
      ref={dropdownRef}
      className={cn('relative', className)}
      data-testid={testId || 'sort-dropdown'}
    >
      {/* Trigger Button */}
      <button
        type="button"
        onClick={() => setIsOpen((prev) => !prev)}
        onKeyDown={handleKeyDown}
        className={cn(
          'flex items-center gap-2',
          'h-10 px-3 rounded-lg',
          'bg-muted/80 hover:bg-muted',
          'text-muted-foreground hover:text-foreground',
          'transition-all duration-200',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          'min-h-[44px] min-w-[44px]'
        )}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-label={`Sort by ${currentOption.label}`}
      >
        {CurrentIcon && <CurrentIcon className="w-4 h-4" />}
        <span className="text-sm font-medium hidden sm:inline">{currentOption.label}</span>
        <ChevronDown
          className={cn(
            'w-4 h-4 transition-transform duration-200',
            isOpen && 'rotate-180'
          )}
        />
      </button>

      {/* Dropdown Menu */}
      {isOpen && (
        <div
          role="listbox"
          aria-label="Sort options"
          className={cn(
            'absolute right-0 mt-2 z-50',
            'w-48 rounded-lg',
            'bg-popover border border-border',
            'shadow-lg',
            'animate-in fade-in-0 zoom-in-95',
            'origin-top-right',
            'py-1'
          )}
        >
          {options.map((option) => {
            const Icon = option.icon;
            const isActive = option.value === value;

            return (
              <button
                key={option.value}
                type="button"
                role="option"
                aria-selected={isActive}
                onClick={() => {
                  onChange(option.value);
                  setIsOpen(false);
                }}
                className={cn(
                  'w-full flex items-center gap-3 px-3 py-2',
                  'text-sm text-left',
                  'transition-colors duration-150',
                  isActive
                    ? 'bg-primary/10 text-primary font-medium'
                    : 'text-foreground hover:bg-muted/60'
                )}
              >
                {Icon && <Icon className="w-4 h-4" />}
                <span className="flex-1">{option.label}</span>
                {isActive && <Check className="w-4 h-4" />}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
