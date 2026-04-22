'use client';
import { useState } from 'react';

import { GameDrawerContent, type GameDrawerGame } from '@/components/library/v2/GameDrawerContent';
import { LibraryDesktop, type LibraryDesktopItem } from '@/components/library/v2/LibraryDesktop';
import { LibraryMobile, type LibraryGameItem } from '@/components/library/v2/LibraryMobile';
import { Drawer, DrawerContent, DrawerHeader, DrawerTitle } from '@/components/ui/v2/drawer/drawer';
import { useDrawerBreakpoint } from '@/components/ui/v2/drawer/use-drawer-breakpoint';

export interface LibraryV2Item extends LibraryGameItem, GameDrawerGame {}

export interface LibraryV2ClientProps {
  readonly items: ReadonlyArray<LibraryV2Item>;
}

export function LibraryV2Client({ items }: LibraryV2ClientProps): React.JSX.Element {
  const breakpoint = useDrawerBreakpoint();
  const [openId, setOpenId] = useState<string | null>(null);
  const openGame = items.find(i => i.id === openId) ?? null;

  if (breakpoint === 'desktop') {
    return <LibraryDesktop items={items as ReadonlyArray<LibraryDesktopItem>} />;
  }

  return (
    <>
      <LibraryMobile items={items} onSelect={setOpenId} />
      <Drawer
        open={openId !== null}
        onOpenChange={o => {
          if (!o) setOpenId(null);
        }}
        side="mobile-bottom"
        entity="game"
      >
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>{openGame?.title ?? 'Dettaglio'}</DrawerTitle>
          </DrawerHeader>
          {openGame && <GameDrawerContent game={openGame} />}
        </DrawerContent>
      </Drawer>
    </>
  );
}
