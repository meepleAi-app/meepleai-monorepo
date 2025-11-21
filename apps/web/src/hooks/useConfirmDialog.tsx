"use client"

import * as React from "react"
import { ConfirmDialog, type ConfirmDialogProps } from "@/components/ui/confirm-dialog"

export interface ConfirmOptions {
  title: string
  message: string
  variant?: "default" | "destructive"
  confirmText?: string
  cancelText?: string
}

interface ConfirmState {
  isOpen: boolean
  title: string
  message: string
  variant: "default" | "destructive"
  confirmText: string
  cancelText: string
}

/**
 * useConfirmDialog - Hook for programmatic confirmation dialogs
 *
 * Returns a Promise-based confirm function that shows a custom dialog
 * instead of window.confirm(). Compatible with async/await patterns.
 *
 * Features:
 * - Promise-based API (returns boolean)
 * - Fully testable (no browser APIs)
 * - SSR-safe
 * - Customizable appearance and text
 *
 * @example
 * ```tsx
 * function MyComponent() {
 *   const { confirm, ConfirmDialogComponent } = useConfirmDialog()
 *
 *   const handleDelete = async () => {
 *     const confirmed = await confirm({
 *       title: "Delete item?",
 *       message: "This action cannot be undone.",
 *       variant: "destructive",
 *       confirmText: "Delete",
 *       cancelText: "Cancel"
 *     })
 *
 *     if (confirmed) {
 *       // Proceed with deletion
 *     }
 *   }
 *
 *   return (
 *     <>
 *       <button onClick={handleDelete}>Delete</button>
 *       <ConfirmDialogComponent />
 *     </>
 *   )
 * }
 * ```
 *
 * @returns Object containing:
 * - confirm: Function that shows dialog and returns Promise<boolean>
 * - ConfirmDialogComponent: React component to render in JSX
 *
 * @important Concurrent Calls
 * If multiple confirm() calls are made concurrently (without awaiting),
 * only the most recent dialog will be displayed. Previous pending dialogs
 * will be superseded. Always await each call before making the next.
 *
 * @example Correct usage (sequential)
 * ```tsx
 * await confirm({ title: "First" })
 * await confirm({ title: "Second" })
 * ```
 *
 * @example Avoid (concurrent - first promise may not resolve as expected)
 * ```tsx
 * confirm({ title: "First" })
 * confirm({ title: "Second" })
 * ```
 */
export function useConfirmDialog() {
  // Use ref to store resolve callback to prevent unnecessary re-renders
  const resolveRef = React.useRef<((value: boolean) => void) | null>(null)

  const [state, setState] = React.useState<ConfirmState>({
    isOpen: false,
    title: "",
    message: "",
    variant: "default",
    confirmText: "Confirm",
    cancelText: "Cancel",
  })

  const confirm = React.useCallback((options: ConfirmOptions): Promise<boolean> => {
    return new Promise<boolean>((resolve) => {
      resolveRef.current = resolve
      setState({
        isOpen: true,
        title: options.title,
        message: options.message,
        variant: options.variant ?? "default",
        confirmText: options.confirmText ?? "Confirm",
        cancelText: options.cancelText ?? "Cancel",
      })
    })
  }, [])

  const handleConfirm = React.useCallback(() => {
    resolveRef.current?.(true)
    resolveRef.current = null
    setState((prev) => ({ ...prev, isOpen: false }))
  }, [])

  const handleCancel = React.useCallback(() => {
    resolveRef.current?.(false)
    resolveRef.current = null
    setState((prev) => ({ ...prev, isOpen: false }))
  }, [])

  const handleOpenChange = React.useCallback((open: boolean) => {
    if (!open) {
      // Dialog was closed via overlay or escape key - treat as cancel
      resolveRef.current?.(false)
      resolveRef.current = null
      setState((prev) => ({ ...prev, isOpen: false }))
    }
  }, [])

  const ConfirmDialogComponent = React.useCallback(() => {
    return (
      <ConfirmDialog
        open={state.isOpen}
        onOpenChange={handleOpenChange}
        title={state.title}
        message={state.message}
        variant={state.variant}
        confirmText={state.confirmText}
        cancelText={state.cancelText}
        onConfirm={handleConfirm}
        onCancel={handleCancel}
      />
    )
  }, [state, handleOpenChange, handleConfirm, handleCancel])

  return {
    confirm,
    ConfirmDialogComponent,
  }
}
