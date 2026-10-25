/* eslint-disable react-refresh/only-export-components */
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import { Icon } from './Icon'

export type ToastType = 'success' | 'error' | 'info' | 'warning'

export type ToastContextValue = {
  showToast: (message: string, type?: ToastType) => void
}

export const ToastContext = createContext<ToastContextValue | undefined>(undefined)

export function useToast(): ToastContextValue {
  const context = useContext(ToastContext)
  if (!context) throw new Error('useToast must be used within a ToastProvider')
  return context
}

type Toast = {
  id: number
  message: string
  type: ToastType
}

let toastId = 0

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])

  const showToast = useCallback((message: string, type: ToastType = 'info') => {
    const id = ++toastId
    setToasts((current) => [...current, { id, message, type }])
  }, [])

  const removeToast = useCallback((id: number) => {
    setToasts((current) => current.filter((toast) => toast.id !== id))
  }, [])

  useEffect(() => {
    if (toasts.length === 0) return
    const timers = toasts.map((toast) => setTimeout(() => removeToast(toast.id), 4000))
    return () => timers.forEach(clearTimeout)
  }, [toasts, removeToast])

  const value = useMemo(() => ({ showToast }), [showToast])

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="toast-container" aria-live="polite">
        {toasts.map((toast) => (
          <div
            key={toast.id}
            aria-live={toast.type === "error" ? "assertive" : "polite"}
            className={`toast toast-${toast.type}`}
            role={toast.type === "error" ? "alert" : "status"}
          >
            <span>{toast.message}</span>
            <button
              type="button"
              onClick={() => removeToast(toast.id)}
              aria-label="Close notification"
              data-tooltip="Dismiss notification"
              title="Dismiss notification"
            >
              <Icon className="app-icon" name="x" />
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}
