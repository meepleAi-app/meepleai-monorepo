"use client"

import * as React from "react"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { AlertCircle, CheckCircle, Info, AlertTriangle, Loader2 } from "lucide-react"

export type AlertVariant = "info" | "success" | "warning" | "error" | "loading"

export interface AlertDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  message: string
  variant?: AlertVariant
  buttonText?: string
  onClose?: () => void
}

/**
 * AlertDialog - Custom alert dialog component
 *
 * Replaces window.alert() with a styled, testable dialog.
 * Features:
 * - Customizable title, message, and button text
 * - Variant support (info/success/warning/error/loading)
 * - Fully testable (no browser APIs)
 * - SSR-safe
 * - Accessible (keyboard navigation, screen readers, ARIA labels)
 *
 * @example
 * ```tsx
 * <AlertDialog
 *   open={isOpen}
 *   onOpenChange={setIsOpen}
 *   title="Success"
 *   message="Your changes have been saved."
 *   variant="success"
 * />
 * ```
 */
export function AlertDialog({
  open,
  onOpenChange,
  title,
  message,
  variant = "info",
  buttonText = "OK",
  onClose,
}: AlertDialogProps) {
  const handleClose = () => {
    onClose?.()
    onOpenChange(false)
  }

  const iconMap: Record<AlertVariant, React.ReactNode> = {
    info: <Info className="h-5 w-5 text-blue-600 dark:text-blue-400" />,
    success: <CheckCircle className="h-5 w-5 text-green-600 dark:text-green-400" />,
    warning: <AlertTriangle className="h-5 w-5 text-yellow-600 dark:text-yellow-400" />,
    error: <AlertCircle className="h-5 w-5 text-red-600 dark:text-red-400" />,
    loading: <Loader2 className="h-5 w-5 text-gray-600 dark:text-gray-400 animate-spin" />,
  }

  const bgColorMap: Record<AlertVariant, string> = {
    info: "bg-blue-100 dark:bg-blue-900/20",
    success: "bg-green-100 dark:bg-green-900/20",
    warning: "bg-yellow-100 dark:bg-yellow-900/20",
    error: "bg-red-100 dark:bg-red-900/20",
    loading: "bg-gray-100 dark:bg-gray-900/20",
  }

  const iconLabelMap: Record<AlertVariant, string> = {
    info: "Information",
    success: "Success",
    warning: "Warning",
    error: "Error",
    loading: "Loading",
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent hideCloseButton className="sm:max-w-[425px]">
        <DialogHeader>
          <div className="flex items-center gap-3">
            <div
              className={`flex h-10 w-10 items-center justify-center rounded-full ${bgColorMap[variant]}`}
              role="img"
              aria-label={`${iconLabelMap[variant]} icon`}
            >
              {iconMap[variant]}
            </div>
            <DialogTitle className="flex-1">{title}</DialogTitle>
          </div>
          <DialogDescription className="text-left">{message}</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button
            type="button"
            onClick={handleClose}
            autoFocus
            className="w-full sm:w-auto"
          >
            {buttonText}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
