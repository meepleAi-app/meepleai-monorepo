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

interface ConfirmState extends ConfirmOptions {
  isOpen: boolean
  resolve: ((value: boolean) => void) | null
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
 */
export function useConfirmDialog() {
  const [state, setState] = React.useState<ConfirmState>({
    isOpen: false,
    title: "",
    message: "",
    variant: "default",
    confirmText: "Confirm",
    cancelText: "Cancel",
    resolve: null,
  })

  const confirm = React.useCallback((options: ConfirmOptions): Promise<boolean> => {
    return new Promise<boolean>((resolve) => {
      setState({
        isOpen: true,
        title: options.title,
        message: options.message,
        variant: options.variant ?? "default",
        confirmText: options.confirmText ?? "Confirm",
        cancelText: options.cancelText ?? "Cancel",
        resolve,
      })
    })
  }, [])

  const handleConfirm = React.useCallback(() => {
    state.resolve?.(true)
    setState((prev) => ({ ...prev, isOpen: false, resolve: null }))
  }, [state.resolve])

  const handleCancel = React.useCallback(() => {
    state.resolve?.(false)
    setState((prev) => ({ ...prev, isOpen: false, resolve: null }))
  }, [state.resolve])

  const handleOpenChange = React.useCallback((open: boolean) => {
    if (!open) {
      // Dialog was closed via overlay or escape key - treat as cancel
      state.resolve?.(false)
      setState((prev) => ({ ...prev, isOpen: false, resolve: null }))
    }
  }, [state.resolve])

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
