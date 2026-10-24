import { useEffect, useState } from 'react'

export type Theme = 'light' | 'dark'

const THEME_STORAGE_KEY = 'civicflow.theme'
const THEME_CHANGE_EVENT = 'civicflow:theme-change'

function isTheme(value: string | null): value is Theme {
  return value === 'light' || value === 'dark'
}

export function readStoredTheme(): Theme | null {
  try {
    const stored = window.localStorage.getItem(THEME_STORAGE_KEY)
    return isTheme(stored) ? stored : null
  } catch {
    return null
  }
}

export function systemTheme(): Theme {
  return window.matchMedia('(prefers-color-scheme: dark)').matches
    ? 'dark'
    : 'light'
}

export function resolveTheme(): Theme {
  return readStoredTheme() ?? systemTheme()
}

export function applyTheme(theme: Theme): void {
  document.documentElement.dataset.theme = theme
  document.documentElement.style.colorScheme = theme
}

function storeTheme(theme: Theme): void {
  try {
    window.localStorage.setItem(THEME_STORAGE_KEY, theme)
  } catch {
    // Persisting the preference is best-effort; the theme still applies.
  }
}

export function useTheme() {
  const [theme, setThemeState] = useState<Theme>(() => resolveTheme())

  useEffect(() => {
    applyTheme(theme)

    function syncTheme(event: Event) {
      const nextTheme = (event as CustomEvent<Theme>).detail
      if (isTheme(nextTheme)) setThemeState(nextTheme)
    }

    window.addEventListener(THEME_CHANGE_EVENT, syncTheme)
    return () => window.removeEventListener(THEME_CHANGE_EVENT, syncTheme)
  }, [theme])

  useEffect(() => {
    const media = window.matchMedia('(prefers-color-scheme: dark)')

    function followSystemTheme() {
      if (readStoredTheme()) return
      const nextTheme = media.matches ? 'dark' : 'light'
      setThemeState(nextTheme)
      window.dispatchEvent(new CustomEvent(THEME_CHANGE_EVENT, { detail: nextTheme }))
    }

    media.addEventListener('change', followSystemTheme)
    return () => media.removeEventListener('change', followSystemTheme)
  }, [])

  function setTheme(nextTheme: Theme) {
    storeTheme(nextTheme)
    setThemeState(nextTheme)
    window.dispatchEvent(new CustomEvent(THEME_CHANGE_EVENT, { detail: nextTheme }))
  }

  function toggleTheme() {
    setTheme(theme === 'dark' ? 'light' : 'dark')
  }

  return { theme, setTheme, toggleTheme }
}
