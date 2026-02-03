/**
 * UnifiedActionBar Component
 * Issue #3479 - Layout System v2: Unified ActionBar
 *
 * Bottom navigation bar that combines:
 * - Primary navigation items (Home, Library, Catalog, etc.)
 * - Context-specific actions (Add, Filter, Play, etc.)
 * - Overflow menu for extra items
 *
 * Replaces separate BottomNav + ActionBar with a single unified component.
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
import {
  useUnifiedActionBar,
  type UnifiedItem,
} from '@/hooks/useUnifiedActionBar';
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

export interface UnifiedActionBarProps
  extends Omit<ComponentPropsWithoutRef<'nav'>, 'children'> {
  /** Animation stagger delay between items (in ms) */
  staggerDelay?: number;
}

/**
 * UnifiedActionBar Component
 *
 * Single bottom bar combining navigation and context actions.
 */
export const UnifiedActionBar = forwardRef<HTMLElement, UnifiedActionBarProps>(
  function UnifiedActionBar({ staggerDelay = 50, className, ...props }, ref) {
    const {
      visibleNavItems,
      visibleContextActions,
      overflowItems,
      hasOverflow,
      isVisible,
      breakpoint,
      handleItemClick,
    } = useUnifiedActionBar();

    // Don't render if not visible
    if (!isVisible) {
      return null;
    }

    // Calculate animation delays
    const navItemCount = visibleNavItems.length;

    return (
      <nav
        ref={ref}
        className={cn(
          // Fixed positioning at bottom
          'fixed bottom-0 left-0 right-0 z-40',

          // Height with safe area
          'h-[72px]',
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
          {/* Navigation Items */}
          {visibleNavItems.map((item, index) => (
            <UnifiedActionBarItem
              key={item.id}
              item={item}
              onClick={() => handleItemClick(item)}
              animationDelay={index * staggerDelay}
              variant={breakpoint === 'desktop' ? 'icon-label' : 'icon-label'}
            />
          ))}

          {/* Separator between nav and actions (desktop only) */}
          {breakpoint === 'desktop' && visibleContextActions.length > 0 && (
            <div
              className="h-8 w-px bg-border/50 mx-1"
              aria-hidden="true"
            />
          )}

          {/* Context Actions */}
          {visibleContextActions.map((item, index) => (
            <UnifiedActionBarItem
              key={item.id}
              item={item}
              onClick={() => handleItemClick(item)}
              animationDelay={(navItemCount + index) * staggerDelay}
              variant={breakpoint === 'desktop' ? 'icon-label' : 'icon-only'}
            />
          ))}

          {/* Overflow Menu */}
          {hasOverflow && (
            <UnifiedOverflowMenu
              items={overflowItems}
              onItemClick={handleItemClick}
              animationDelay={(navItemCount + visibleContextActions.length) * staggerDelay}
            />
          )}
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
      className={cn(
        'h-[72px]',
        'pb-[env(safe-area-inset-bottom)]',
        className
      )}
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
        <span className="text-[10px] font-medium truncate max-w-[56px]">
          {item.label}
        </span>
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

function UnifiedOverflowMenu({
  items,
  onItemClick,
  animationDelay,
}: UnifiedOverflowMenuProps) {
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
      <DropdownMenuContent
        align="end"
        side="top"
        sideOffset={8}
        className="w-48"
      >
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
        {navItems.length > 0 && actionItems.length > 0 && (
          <DropdownMenuSeparator />
        )}

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
