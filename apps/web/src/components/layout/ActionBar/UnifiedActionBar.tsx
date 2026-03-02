/**
 * UnifiedActionBar Component
 * Issue #3479 - Layout System v2: Unified ActionBar
 *
 * Mobile-only bottom navigation bar that combines:
 * - Primary navigation items (Home, Library, Chat, Toolkit, etc.)
 * - Integrated FAB for context-specific primary action
 * - Overflow menu for extra items
 *
 * Desktop navigation is handled by UnifiedHeader.
 * This component replaces both BottomNav and SmartFAB.
 */

'use client';

import { forwardRef, type ComponentPropsWithoutRef } from 'react';

import {
  Home,
  BookOpen,
  Gamepad2,
  MessageSquare,
  User,
  Plus,
  Filter,
  ArrowUpDown,
  LayoutGrid,
  Download,
  Play,
  Heart,
  Share,
  Flag,
  Timer,
  Trophy,
  Pause,
  Square,
  Send,
  Paperclip,
  Mic,
  PlusCircle,
  History,
  Bookmark,
  Save,
  RotateCcw,
  MoreVertical,
  Dice6,
  // Extended icons for Layout System v2
  BarChart2,
  Trash2,
  UserPlus,
  X,
  ZoomIn,
  ZoomOut,
  CheckCheck,
  Settings,
  Edit,
  type LucideIcon,
} from 'lucide-react';
import Link from 'next/link';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { useFAB } from '@/hooks/useFAB';
import { useLongPress } from '@/hooks/useLongPress';
import { useUnifiedActionBar, type UnifiedItem } from '@/hooks/useUnifiedActionBar';
import { cn } from '@/lib/utils';

/**
 * Icon mapping for all supported icons
 */
const ICON_MAP: Record<string, LucideIcon> = {
  // Navigation icons
  home: Home,
  'book-open': BookOpen,
  'gamepad-2': Gamepad2,
  'message-square': MessageSquare,
  user: User,
  'dice-6': Dice6,
  // Action icons
  plus: Plus,
  filter: Filter,
  'arrow-up-down': ArrowUpDown,
  'layout-grid': LayoutGrid,
  download: Download,
  play: Play,
  heart: Heart,
  share: Share,
  flag: Flag,
  timer: Timer,
  trophy: Trophy,
  pause: Pause,
  square: Square,
  send: Send,
  paperclip: Paperclip,
  mic: Mic,
  'plus-circle': PlusCircle,
  history: History,
  bookmark: Bookmark,
  save: Save,
  'rotate-ccw': RotateCcw,
  // Extended icons for Layout System v2
  'bar-chart-2': BarChart2,
  'trash-2': Trash2,
  'user-plus': UserPlus,
  x: X,
  'zoom-in': ZoomIn,
  'zoom-out': ZoomOut,
  'check-check': CheckCheck,
  settings: Settings,
  edit: Edit,
};

export interface UnifiedActionBarProps extends Omit<ComponentPropsWithoutRef<'nav'>, 'children'> {
  /** Animation stagger delay between items (in ms) */
  staggerDelay?: number;
}

/**
 * UnifiedActionBar Component
 *
 * Mobile-only bottom bar combining navigation and integrated FAB.
 * Desktop navigation is handled by UnifiedHeader.
 */
export const UnifiedActionBar = forwardRef<HTMLElement, UnifiedActionBarProps>(
  function UnifiedActionBar({ staggerDelay = 50, className, ...props }, ref) {
    const {
      visibleNavItems,
      overflowItems: _overflowItems,
      hasOverflow: _hasOverflow,
      isVisible,
      breakpoint,
      handleItemClick,
    } = useUnifiedActionBar();

    // FAB integration
    const {
      config: fabConfig,
      isQuickMenuOpen,
      openQuickMenu,
      closeQuickMenu,
      triggerAction: triggerFABAction,
      triggerQuickAction,
    } = useFAB();

    // Long press for FAB quick menu
    const fabLongPressHandlers = useLongPress(openQuickMenu, {
      onClick: triggerFABAction,
    });

    // Don't render on desktop - navigation is in UnifiedHeader
    if (breakpoint === 'desktop') {
      return null;
    }

    // Don't render if not visible
    if (!isVisible) {
      return null;
    }

    // Calculate animation delays
    const navItemCount = visibleNavItems.length;

    // Get FAB icon
    const FABIconComponent = fabConfig ? (ICON_MAP[fabConfig.icon] ?? Plus) : Plus;

    return (
      <nav
        ref={ref}
        className={cn(
          // Fixed positioning at bottom
          'fixed bottom-0 left-0 right-0 z-40',
          // Hide on desktop (md+)
          'md:hidden',

          // Height with safe area
          'h-[56px]',
          'pb-[env(safe-area-inset-bottom)]',

          // Glass morphism effect (matches existing design)
          'bg-background/95 dark:bg-card/95',
          'backdrop-blur-[16px] backdrop-saturate-[180%]',
          'dark:backdrop-blur-none',

          // Border and shadow
          'border-t border-border/50 dark:border-border/30',
          'shadow-lg dark:shadow-2xl dark:shadow-black/20',

          className
        )}
        aria-label="Navigazione principale e azioni"
        {...props}
      >
        <div
          className={cn(
            'h-full max-w-screen-lg mx-auto',
            'flex items-center justify-around',
            'px-2 sm:px-4'
          )}
        >
          {/* Navigation Items — split around central FAB */}
          {/* Items before FAB (e.g., Home, Libreria) */}
          {visibleNavItems.slice(0, 2).map((item, index) => (
            <UnifiedActionBarItem
              key={item.id}
              item={item}
              onClick={() => handleItemClick(item)}
              animationDelay={index * staggerDelay}
              variant="icon-label"
            />
          ))}

          {/* Central FAB — Chat/AI action */}
          {fabConfig ? (
            <div className="relative">
              {/* Quick Menu (shown on long press) */}
              {isQuickMenuOpen && (
                <div
                  className={cn(
                    'absolute bottom-full mb-2 right-1/2 translate-x-1/2',
                    'flex flex-col gap-2 p-2',
                    'bg-background/95 dark:bg-card/95',
                    'backdrop-blur-[16px]',
                    'rounded-xl border border-border/50',
                    'shadow-lg',
                    'animate-in fade-in-0 slide-in-from-bottom-2'
                  )}
                >
                  {fabConfig.quickMenuItems.map(item => {
                    const QuickIcon = ICON_MAP[item.icon] ?? Plus;
                    return (
                      <button
                        key={item.id}
                        type="button"
                        onClick={() => {
                          triggerQuickAction(item.id);
                          closeQuickMenu();
                        }}
                        className={cn(
                          'flex items-center gap-2 px-3 py-2',
                          'text-sm text-muted-foreground',
                          'hover:text-primary hover:bg-muted/50',
                          'rounded-lg transition-colors'
                        )}
                      >
                        <QuickIcon className="h-4 w-4" />
                        <span>{item.label}</span>
                      </button>
                    );
                  })}
                </div>
              )}

              {/* FAB Button */}
              <button
                type="button"
                className={cn(
                  'flex flex-col items-center justify-center gap-0.5',
                  'w-12 h-12 -mt-3',
                  'rounded-full',
                  'bg-primary text-primary-foreground',
                  'shadow-lg hover:shadow-xl',
                  'transition-all duration-200',
                  'hover:scale-105 active:scale-95',
                  'focus-visible:outline-none focus-visible:ring-2',
                  'focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2',
                  'animate-in fade-in-0 slide-in-from-bottom-2',
                  isQuickMenuOpen && 'rotate-45'
                )}
                style={{
                  animationDelay: `${navItemCount * staggerDelay}ms`,
                  animationFillMode: 'backwards',
                }}
                aria-label={fabConfig.label}
                aria-expanded={isQuickMenuOpen}
                aria-haspopup="menu"
                data-testid="actionbar-fab"
                {...fabLongPressHandlers}
              >
                <FABIconComponent
                  className={cn(
                    'h-5 w-5',
                    'transition-transform duration-200',
                    isQuickMenuOpen && 'rotate-45'
                  )}
                  aria-hidden="true"
                />
              </button>
            </div>
          ) : (
            /* Fallback central FAB linking to /chat */
            <Link
              href="/chat"
              className={cn(
                'flex items-center justify-center',
                'w-12 h-12 -mt-3',
                'rounded-full',
                'bg-primary text-primary-foreground',
                'shadow-lg hover:shadow-xl',
                'transition-all duration-200',
                'hover:scale-105 active:scale-95',
                'focus-visible:outline-none focus-visible:ring-2',
                'focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2'
              )}
              aria-label="Open chat"
              data-testid="actionbar-fab"
            >
              <MessageSquare className="h-5 w-5" aria-hidden="true" />
            </Link>
          )}

          {/* Items after FAB (e.g., Catalogo) */}
          {visibleNavItems.slice(2).map((item, index) => (
            <UnifiedActionBarItem
              key={item.id}
              item={item}
              onClick={() => handleItemClick(item)}
              animationDelay={(index + 3) * staggerDelay}
              variant="icon-label"
            />
          ))}
        </div>
      </nav>
    );
  }
);

UnifiedActionBar.displayName = 'UnifiedActionBar';

/**
 * UnifiedActionBarSpacer Component
 *
 * Spacer to prevent content from being hidden behind the ActionBar.
 */
export function UnifiedActionBarSpacer({ className }: { className?: string }) {
  const { isVisible } = useUnifiedActionBar();

  if (!isVisible) {
    return null;
  }

  return (
    <div
      className={cn('h-[56px]', 'pb-[env(safe-area-inset-bottom)]', className)}
      aria-hidden="true"
    />
  );
}

// ============================================================================
// Internal Components
// ============================================================================

interface UnifiedActionBarItemProps {
  item: UnifiedItem;
  onClick: () => void;
  animationDelay: number;
  variant: 'icon-only' | 'icon-label';
}

function UnifiedActionBarItem({
  item,
  onClick,
  animationDelay,
  variant,
}: UnifiedActionBarItemProps) {
  const IconComponent = ICON_MAP[item.icon] ?? Home;

  const content = (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-0.5',
        'min-w-[44px] min-h-[44px]', // Touch target WCAG 2.1 AA
        'px-2 py-1',
        'rounded-lg',
        'transition-colors duration-200',
        'cursor-pointer',

        // Focus states
        'focus-visible:outline-none focus-visible:ring-2',
        'focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2',

        // Active state for nav items
        item.isActive
          ? 'text-[hsl(262_83%_62%)] font-semibold'
          : 'text-muted-foreground hover:text-primary hover:bg-muted/50',

        // Animation
        'animate-in fade-in-0 slide-in-from-bottom-2'
      )}
      style={{
        animationDelay: `${animationDelay}ms`,
        animationFillMode: 'backwards',
      }}
    >
      <IconComponent
        className={cn(
          'h-5 w-5',
          item.variant === 'primary' && 'text-primary',
          item.variant === 'destructive' && 'text-destructive'
        )}
        aria-hidden="true"
      />
      {variant === 'icon-label' && (
        <span className="text-[10px] font-medium truncate max-w-[56px]">{item.label}</span>
      )}
    </div>
  );

  // Navigation items use Link
  if (item.type === 'nav' && item.href) {
    return (
      <Link
        href={item.href}
        aria-label={item.ariaLabel ?? item.label}
        aria-current={item.isActive ? 'page' : undefined}
        data-testid={item.testId}
      >
        {content}
      </Link>
    );
  }

  // Action items use button
  return (
    <TooltipProvider delayDuration={300}>
      <Tooltip>
        <TooltipTrigger asChild>
          <button
            type="button"
            onClick={onClick}
            aria-label={item.ariaLabel ?? item.label}
            data-testid={item.testId}
          >
            {content}
          </button>
        </TooltipTrigger>
        {variant === 'icon-only' && (
          <TooltipContent side="top" sideOffset={8}>
            <p>{item.label}</p>
          </TooltipContent>
        )}
      </Tooltip>
    </TooltipProvider>
  );
}

interface UnifiedOverflowMenuProps {
  items: UnifiedItem[];
  onItemClick: (item: UnifiedItem) => void;
  animationDelay: number;
}

function _UnifiedOverflowMenu({ items, onItemClick, animationDelay }: UnifiedOverflowMenuProps) {
  // Separate nav items and action items for menu sections
  const navItems = items.filter(item => item.type === 'nav');
  const actionItems = items.filter(item => item.type === 'action');

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className={cn(
            'flex flex-col items-center justify-center gap-0.5',
            'min-w-[44px] min-h-[44px]',
            'px-2 py-1',
            'rounded-lg',
            'text-muted-foreground hover:text-primary hover:bg-muted/50',
            'transition-colors duration-200',
            'focus-visible:outline-none focus-visible:ring-2',
            'focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2',
            'animate-in fade-in-0 slide-in-from-bottom-2'
          )}
          style={{
            animationDelay: `${animationDelay}ms`,
            animationFillMode: 'backwards',
          }}
          aria-label="Altre opzioni"
          data-testid="actionbar-overflow"
        >
          <MoreVertical className="h-5 w-5" aria-hidden="true" />
          <span className="text-[10px] font-medium">Altro</span>
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" side="top" sideOffset={8} className="w-48">
        {/* Navigation items section */}
        {navItems.length > 0 && (
          <>
            {navItems.map(item => {
              const IconComponent = ICON_MAP[item.icon] ?? Home;
              return (
                <DropdownMenuItem
                  key={item.id}
                  onClick={() => onItemClick(item)}
                  className="flex items-center gap-2 cursor-pointer"
                  data-testid={item.testId}
                >
                  <IconComponent className="h-4 w-4" aria-hidden="true" />
                  <span>{item.label}</span>
                </DropdownMenuItem>
              );
            })}
          </>
        )}

        {/* Separator between nav and actions */}
        {navItems.length > 0 && actionItems.length > 0 && <DropdownMenuSeparator />}

        {/* Action items section */}
        {actionItems.map(item => {
          const IconComponent = ICON_MAP[item.icon] ?? Plus;
          return (
            <DropdownMenuItem
              key={item.id}
              onClick={() => onItemClick(item)}
              className={cn(
                'flex items-center gap-2 cursor-pointer',
                item.variant === 'destructive' && 'text-destructive focus:text-destructive'
              )}
              data-testid={item.testId}
            >
              <IconComponent className="h-4 w-4" aria-hidden="true" />
              <span>{item.label}</span>
            </DropdownMenuItem>
          );
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
