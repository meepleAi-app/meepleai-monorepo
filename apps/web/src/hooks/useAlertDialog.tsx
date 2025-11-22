"use client"

import * as React from "react"
import { AlertDialog, type AlertDialogProps, type AlertVariant } from "@/components/ui/alert-dialog"

export interface AlertOptions {
  title: string
  message: string
  variant?: AlertVariant
  buttonText?: string
}

interface AlertState {
  isOpen: boolean
  title: string
  message: string
  variant: AlertVariant
  buttonText: string
}

/**
 * useAlertDialog - Hook for programmatic alert dialogs
 *
 * Returns a Promise-based alert function that shows a custom dialog
 * instead of window.alert(). Compatible with async/await patterns.
 *
 * Features:
 * - Promise-based API (resolves when closed)
 * - Fully testable (no browser APIs)
 * - SSR-safe
 * - Customizable appearance and text
 * - Five variants: info, success, warning, error, loading
 *
 * @example
 * ```tsx
 * function MyComponent() {
 *   const { alert, AlertDialogComponent } = useAlertDialog()
 *
 *   const handleSave = async () => {
 *     try {
 *       await saveData()
 *       await alert({
 *         title: "Success",
 *         message: "Your changes have been saved.",
 *         variant: "success"
 *       })
 *     } catch (error) {
 *       await alert({
 *         title: "Error",
 *         message: "Failed to save changes.",
 *         variant: "error"
 *       })
 *     }
 *   }
 *
 *   return (
 *     <>
 *       <button onClick={handleSave}>Save</button>
 *       <AlertDialogComponent />
 *     </>
 *   )
 * }
 * ```
 *
 * @returns Object containing:
 * - alert: Function that shows dialog and returns Promise<void>
 * - AlertDialogComponent: React component to render in JSX
 *
 * @important Concurrent Calls
 * If multiple alert() calls are made concurrently (without awaiting),
 * only the most recent dialog will be displayed. Previous pending dialogs
 * will be superseded. Always await each call before making the next.
 *
 * @example Correct usage (sequential)
 * ```tsx
 * await alert({ title: "First", message: "First message", variant: "info" })
 * await alert({ title: "Second", message: "Second message", variant: "success" })
 * ```
 *
 * @example Avoid (concurrent - first promise may not resolve as expected)
 * ```tsx
 * alert({ title: "First", message: "First message" })
 * alert({ title: "Second", message: "Second message" })
 * ```
 */
export function useAlertDialog() {
  // Use ref to store resolve callback to prevent unnecessary re-renders
  const resolveRef = React.useRef<(() => void) | null>(null)

  const [state, setState] = React.useState<AlertState>({
    isOpen: false,
    title: "",
    message: "",
    variant: "info",
    buttonText: "OK",
  })

  const alert = React.useCallback((options: AlertOptions): Promise<void> => {
    return new Promise<void>((resolve) => {
      resolveRef.current = resolve
      setState({
        isOpen: true,
        title: options.title,
        message: options.message,
        variant: options.variant ?? "info",
        buttonText: options.buttonText ?? "OK",
      })
    })
  }, [])

  const handleClose = React.useCallback(() => {
    resolveRef.current?.()
    resolveRef.current = null
    setState((prev) => ({ ...prev, isOpen: false }))
  }, [])

  const handleOpenChange = React.useCallback((open: boolean) => {
    if (!open) {
      // Dialog was closed via overlay or escape key
      resolveRef.current?.()
      resolveRef.current = null
      setState((prev) => ({ ...prev, isOpen: false }))
    }
  }, [])

  const AlertDialogComponent = React.useCallback(() => {
    return (
      <AlertDialog
        open={state.isOpen}
        onOpenChange={handleOpenChange}
        title={state.title}
        message={state.message}
        variant={state.variant}
        buttonText={state.buttonText}
        onClose={handleClose}
      />
    )
  }, [state, handleOpenChange, handleClose])

  return {
    alert,
    AlertDialogComponent,
  }
}
