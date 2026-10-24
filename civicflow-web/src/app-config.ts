import { createContext, useContext } from 'react'

export const DEFAULT_APPLICATION_NAME = 'CivicFlow Enterprise'

export type PublicConfiguration = {
  applicationName: string
}

export type AppConfigValue = PublicConfiguration & {
  loaded: boolean
}

export const AppConfigContext = createContext<AppConfigValue>({
  applicationName: DEFAULT_APPLICATION_NAME,
  loaded: false,
})

export function useAppConfig(): AppConfigValue {
  return useContext(AppConfigContext)
}
