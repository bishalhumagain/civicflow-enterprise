import { createContext } from 'react'

export type ToastType = 'success' | 'error' | 'info' | 'warning'

export type ToastContextValue = {
  showToast: (message: string, type?: ToastType) => void
}

export const ToastContext = createContext<ToastContextValue | undefined>(undefined)
