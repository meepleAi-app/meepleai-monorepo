/**
 * AdvancedFiltersDrawer (Phase 3a #1606).
 *
 * Standalone reusable component that composes the existing `Drawer` primitive
 * with entity-conditional filter sections derived from `entityScope`. Owns its
 * own draft state internally; parent only observes via `onApply` / `onClear`
 * callbacks.
 *
 * Composition: `Drawer` (mobile↔desktop responsive) → `DrawerContent` →
 *   `DrawerHeader` (title + description) →
 *   sections (from `getSectionsForScope`) →
 *   `DrawerFooter` (Cancel / Clear / Apply).
 */

'use client';

import { useState, type ReactElement } from 'react';

import {
  Drawer,
  DrawerContent,
  DrawerDescription,
  DrawerFooter,
  DrawerHeader,
  DrawerTitle,
} from '@/components/ui/drawer';
import { useTranslation } from '@/hooks/useTranslation';

import type { AdvancedFiltersDrawerProps, LibraryFilters } from './types';

export function AdvancedFiltersDrawer({
  open,
  onOpenChange,
  entityScope,
  activeFilters,
  onApply,
  onClear,
}: AdvancedFiltersDrawerProps): ReactElement {
  const { t } = useTranslation();

  // Draft state: initialised from activeFilters; synced to it on cancel.
  // Tasks 4-7 will read/mutate draft via section handlers.
  const [draft, setDraft] = useState<LibraryFilters>(activeFilters);

  // Cancel: discard draft and close drawer.
  const handleCancel = () => {
    setDraft(activeFilters);
    onOpenChange(false);
  };

  // Silence unused-vars until Tasks 4-7 wire these in.
  void onApply;
  void onClear;
  void draft;

  return (
    <Drawer open={open} onOpenChange={onOpenChange} entity={entityScope}>
      <DrawerContent data-slot="advanced-filters-drawer">
        <DrawerHeader>
          <DrawerTitle>{t('pages.library.filters.title')}</DrawerTitle>
          <DrawerDescription>{t('pages.library.filters.description')}</DrawerDescription>
        </DrawerHeader>

        {/* Sections rendered in Tasks 4-6, driven by getSectionsForScope(entityScope) and `draft` */}
        {/* For Task 3, this is intentionally empty — skeleton only */}
        <div data-slot="advanced-filters-sections" aria-hidden="true" />

        <DrawerFooter>
          <button
            type="button"
            data-slot="advanced-filters-cancel"
            onClick={handleCancel}
            className="rounded-md border border-border bg-card px-3 py-1.5 text-sm font-medium text-foreground hover:bg-muted/40"
          >
            {t('common.cancel')}
          </button>
        </DrawerFooter>
      </DrawerContent>
    </Drawer>
  );
}
