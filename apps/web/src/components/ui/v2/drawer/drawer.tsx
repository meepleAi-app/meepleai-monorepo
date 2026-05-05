'use client';

import {
  createContext,
  forwardRef,
  useContext,
  useMemo,
  type ButtonHTMLAttributes,
  type CSSProperties,
  type HTMLAttributes,
  type ReactNode,
} from 'react';

import * as RadixDialog from '@radix-ui/react-dialog';
import clsx from 'clsx';
import { Drawer as VaulDrawer } from 'vaul';

import { useDrawerBreakpoint } from './use-drawer-breakpoint';

import type { EntityType } from '../entity-tokens';

// Mirror EntityCard: `kb` -> `--e-document` (pre-existing token name).
const ENTITY_CSS_VAR_KEY: Record<EntityType, string> = {
  game: 'game',
  player: 'player',
  session: 'session',
  agent: 'agent',
  kb: 'document',
  chat: 'chat',
  event: 'event',
  toolkit: 'toolkit',
  tool: 'tool',
};

export type DrawerSide = 'auto' | 'mobile-bottom' | 'desktop-right';
export type DrawerDirection = 'bottom' | 'right';
type DrawerMode = 'mobile' | 'desktop';

export interface DrawerProps {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly side?: DrawerSide;
  /** Convenience alias: `bottom` -> `mobile-bottom`, `right` -> `desktop-right`. */
  readonly direction?: DrawerDirection;
  readonly entity?: EntityType;
  readonly children: ReactNode;
}

interface DrawerContextValue {
  readonly mode: DrawerMode;
  readonly entity?: EntityType;
}

const DrawerContext = createContext<DrawerContextValue | null>(null);

function useDrawerContext(component: string): DrawerContextValue {
  const ctx = useContext(DrawerContext);
  if (!ctx) throw new Error(`${component} must be rendered inside <Drawer>`);
  return ctx;
}

function resolveMode(
  side: DrawerSide,
  direction: DrawerDirection | undefined,
  breakpoint: DrawerMode
): DrawerMode {
  if (direction === 'bottom') return 'mobile';
  if (direction === 'right') return 'desktop';
  if (side === 'mobile-bottom') return 'mobile';
  if (side === 'desktop-right') return 'desktop';
  return breakpoint;
}

function entityAccentStyle(entity: EntityType | undefined): CSSProperties | undefined {
  if (!entity) return undefined;
  return { backgroundColor: `hsl(var(--e-${ENTITY_CSS_VAR_KEY[entity]}))` };
}

export function Drawer({
  open,
  onOpenChange,
  side = 'auto',
  direction,
  entity,
  children,
}: DrawerProps): React.JSX.Element {
  const breakpoint = useDrawerBreakpoint();
  const mode = resolveMode(side, direction, breakpoint);
  const ctx = useMemo<DrawerContextValue>(() => ({ mode, entity }), [mode, entity]);
  const Root = mode === 'mobile' ? VaulDrawer.Root : RadixDialog.Root;
  return (
    <DrawerContext.Provider value={ctx}>
      <Root open={open} onOpenChange={onOpenChange}>
        {children}
      </Root>
    </DrawerContext.Provider>
  );
}

const OVERLAY_CLS = 'fixed inset-0 z-40 bg-black/40 backdrop-blur-sm';
const MOBILE_CONTENT_CLS =
  'fixed inset-x-0 bottom-0 z-50 flex max-h-[90vh] flex-col rounded-t-2xl bg-card shadow-lg outline-none';
const DESKTOP_CONTENT_CLS =
  'fixed inset-y-0 right-0 z-50 flex w-[480px] max-w-full flex-col rounded-l-2xl bg-card shadow-lg outline-none';

export interface DrawerContentProps extends HTMLAttributes<HTMLDivElement> {
  readonly children: ReactNode;
}

export const DrawerContent = forwardRef<HTMLDivElement, DrawerContentProps>(function DrawerContent(
  { children, className, ...rest },
  ref
) {
  const { mode, entity } = useDrawerContext('DrawerContent');
  if (mode === 'mobile') {
    return (
      <VaulDrawer.Portal>
        <VaulDrawer.Overlay className={OVERLAY_CLS} />
        <VaulDrawer.Content ref={ref} className={clsx(MOBILE_CONTENT_CLS, className)} {...rest}>
          <div
            data-drawer-accent={entity}
            aria-hidden="true"
            className={clsx('mx-auto mt-2 h-1 w-12 rounded-full', !entity && 'bg-border')}
            style={entityAccentStyle(entity)}
          />
          {children}
        </VaulDrawer.Content>
      </VaulDrawer.Portal>
    );
  }
  return (
    <RadixDialog.Portal>
      <RadixDialog.Overlay className={OVERLAY_CLS} />
      <RadixDialog.Content ref={ref} className={clsx(DESKTOP_CONTENT_CLS, className)} {...rest}>
        {entity && (
          <div
            data-drawer-accent={entity}
            aria-hidden="true"
            className="absolute inset-y-0 left-0 w-1 rounded-l-2xl"
            style={entityAccentStyle(entity)}
          />
        )}
        {children}
      </RadixDialog.Content>
    </RadixDialog.Portal>
  );
});

export interface DrawerHeaderProps extends HTMLAttributes<HTMLDivElement> {
  readonly children: ReactNode;
}
export function DrawerHeader({
  children,
  className,
  ...rest
}: DrawerHeaderProps): React.JSX.Element {
  return (
    <div className={clsx('flex flex-col gap-1 p-4', className)} {...rest}>
      {children}
    </div>
  );
}

export interface DrawerFooterProps extends HTMLAttributes<HTMLDivElement> {
  readonly children: ReactNode;
}
export function DrawerFooter({
  children,
  className,
  ...rest
}: DrawerFooterProps): React.JSX.Element {
  return (
    <div className={clsx('mt-auto flex items-center justify-end gap-2 p-4', className)} {...rest}>
      {children}
    </div>
  );
}

export interface DrawerTitleProps extends HTMLAttributes<HTMLHeadingElement> {
  readonly children: ReactNode;
}
export const DrawerTitle = forwardRef<HTMLHeadingElement, DrawerTitleProps>(function DrawerTitle(
  { children, className, ...rest },
  ref
) {
  const { mode } = useDrawerContext('DrawerTitle');
  const Cmp = mode === 'mobile' ? VaulDrawer.Title : RadixDialog.Title;
  return (
    <Cmp ref={ref} className={clsx('text-lg font-semibold text-foreground', className)} {...rest}>
      {children}
    </Cmp>
  );
});

export interface DrawerDescriptionProps extends HTMLAttributes<HTMLParagraphElement> {
  readonly children: ReactNode;
}
export const DrawerDescription = forwardRef<HTMLParagraphElement, DrawerDescriptionProps>(
  function DrawerDescription({ children, className, ...rest }, ref) {
    const { mode } = useDrawerContext('DrawerDescription');
    const Cmp = mode === 'mobile' ? VaulDrawer.Description : RadixDialog.Description;
    return (
      <Cmp ref={ref} className={clsx('text-sm text-muted-foreground', className)} {...rest}>
        {children}
      </Cmp>
    );
  }
);

export interface DrawerCloseProps extends Omit<
  ButtonHTMLAttributes<HTMLButtonElement>,
  'children'
> {
  readonly children: ReactNode;
}
export const DrawerClose = forwardRef<HTMLButtonElement, DrawerCloseProps>(function DrawerClose(
  { children, className, ...rest },
  ref
) {
  const { mode } = useDrawerContext('DrawerClose');
  const Cmp = mode === 'mobile' ? VaulDrawer.Close : RadixDialog.Close;
  const cls = clsx(
    'inline-flex items-center justify-center rounded-md border border-border bg-card px-3 py-1.5 text-sm font-medium text-foreground transition-colors hover:bg-muted/40',
    className
  );
  return (
    <Cmp ref={ref} className={cls} {...rest}>
      {children}
    </Cmp>
  );
});
