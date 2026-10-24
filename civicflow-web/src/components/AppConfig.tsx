import {
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import {
  AppConfigContext,
  DEFAULT_APPLICATION_NAME,
  type PublicConfiguration,
} from '../app-config'

function normalizeName(value: unknown): string {
  return typeof value === 'string' && value.trim()
    ? value.trim()
    : DEFAULT_APPLICATION_NAME
}

export function AppConfigProvider({ children }: { children: ReactNode }) {
  const [configuration, setConfiguration] = useState<PublicConfiguration>({
    applicationName: DEFAULT_APPLICATION_NAME,
  })
  const [loaded, setLoaded] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    const apiBaseUrl =
      (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ?? ''

    void fetch(`${apiBaseUrl}/api/config/public`, {
      headers: { Accept: 'application/json' },
      signal: controller.signal,
    })
      .then((response) => (response.ok ? response.json() : null))
      .then((body: unknown) => {
        if (!body || typeof body !== 'object') return
        const record = body as Record<string, unknown>
        setConfiguration({
          applicationName: normalizeName(record.applicationName),
        })
      })
      .catch(() => {
        // The public configuration is cosmetic. The app still works with the
        // product defaults when the API is unreachable.
      })
      .finally(() => setLoaded(true))

    return () => controller.abort()
  }, [])

  const value = useMemo(
    () => ({ ...configuration, loaded }),
    [configuration, loaded],
  )

  return (
    <AppConfigContext.Provider value={value}>
      {children}
    </AppConfigContext.Provider>
  )
}
