/**
 * Sort dropdown component
 * Issue #3587: GC-002 — Sorting Controls
 */

'use client';

import React, { useEffect, useRef, useState } from 'react';

import { ChevronDown } from 'lucide-react';

import { cn } from '@/lib/utils';

import { CAROUSEL_SORT_OPTIONS } from '../constants';

import type { CarouselSortValue } from '../types';

export function SortDropdown({
  value,
  onChange,
}: {
  value: CarouselSortValue;
  onChange: (value: CarouselSortValue) => void;
}) {
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

  // Close on escape
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

  const currentOption =
    CAROUSEL_SORT_OPTIONS.find(opt => opt.value === value) ?? CAROUSEL_SORT_OPTIONS[0];
  const CurrentIcon = currentOption.icon;

  return (
    <div ref={dropdownRef} className="relative">
      <button
        onClick={() => setIsOpen(prev => !prev)}
        className={cn(
          'flex items-center gap-2',
          'h-10 px-3 rounded-lg',
          'bg-muted/80 hover:bg-muted',
          'text-muted-foreground hover:text-foreground',
          'transition-all duration-200',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          // Mobile-friendly touch target
          'min-h-[44px] min-w-[44px]'
        )}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-label={`Sort by ${currentOption.label}`}
      >
        <CurrentIcon className="w-4 h-4" />
        <span className="text-sm font-medium hidden sm:inline">{currentOption.label}</span>
        <ChevronDown
          className={cn('w-4 h-4 transition-transform duration-200', isOpen && 'rotate-180')}
        />
      </button>

      {/* Dropdown Menu */}
      {isOpen && (
        <div
          className={cn(
            'absolute right-0 mt-2 z-50',
            'w-48 rounded-lg',
            'bg-popover border border-border',
            'shadow-lg',
            // Animation
            'animate-in fade-in-0 zoom-in-95',
            'origin-top-right'
          )}
          role="listbox"
          aria-label="Sort options"
        >
          <div className="p-1">
            {CAROUSEL_SORT_OPTIONS.map(option => {
              const Icon = option.icon;
              const isSelected = option.value === value;

              return (
                <button
                  key={option.value}
                  onClick={() => {
                    onChange(option.value);
                    setIsOpen(false);
                  }}
                  role="option"
                  aria-selected={isSelected}
                  className={cn(
                    'w-full flex items-center gap-3',
                    'px-3 py-2 rounded-md',
                    'text-sm',
                    'transition-colors duration-150',
                    // Mobile-friendly touch target
                    'min-h-[44px]',
                    isSelected
                      ? 'bg-primary/10 text-primary'
                      : 'text-muted-foreground hover:bg-muted hover:text-foreground',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-inset'
                  )}
                >
                  <Icon className="w-4 h-4" />
                  <span className="flex-1 text-left">{option.label}</span>
                  {isSelected && (
                    <span className="text-xs text-primary" aria-hidden="true">
                      ✓
                    </span>
                  )}
                </button>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
