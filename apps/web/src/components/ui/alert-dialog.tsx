/**
 * AlertDialog exports - Issue #2372
 *
 * This file exports both:
 * 1. Radix AlertDialog primitives (AlertDialog, AlertDialogContent, etc.)
 * 2. Custom AlertDialog component (AlertDialogCustom) for simple alerts
 *
 * For confirmation dialogs requiring Cancel/Confirm, use the primitives.
 * For simple notifications/alerts, use AlertDialogCustom.
 */

// Re-export Radix primitives as default AlertDialog components
export {
  AlertDialog,
  AlertDialogPortal,
  AlertDialogOverlay,
  AlertDialogTrigger,
  AlertDialogContent,
  AlertDialogHeader,
  AlertDialogFooter,
  AlertDialogTitle,
  AlertDialogDescription,
  AlertDialogAction,
  AlertDialogCancel,
} from './overlays/alert-dialog-primitives';

// Re-export custom AlertDialog as named export for simple alerts
export {
  AlertDialog as AlertDialogCustom,
  type AlertDialogProps as AlertDialogCustomProps,
  type AlertVariant,
} from './feedback/alert-dialog';
