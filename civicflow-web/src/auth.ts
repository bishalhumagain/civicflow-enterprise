import {
  UserManager,
  WebStorageStateStore,
  type User,
} from 'oidc-client-ts'

export type AuthSession = {
  accessToken: string
  idToken?: string
  expiresAt: number
}

const authority = import.meta.env.VITE_KEYCLOAK_AUTHORITY as string | undefined
const clientId = import.meta.env.VITE_KEYCLOAK_CLIENT_ID as string | undefined
const redirectUri = import.meta.env.VITE_KEYCLOAK_REDIRECT_URI as string | undefined
const redirect = redirectUri ?? window.location.origin

export const oidcIsConfigured = Boolean(authority && clientId)

const userManager = oidcIsConfigured
  ? new UserManager({
      authority: authority!,
      client_id: clientId!,
      redirect_uri: redirect,
      post_logout_redirect_uri: redirect,
      response_type: 'code',
      scope: 'openid profile email',
      userStore: new WebStorageStateStore({ store: window.sessionStorage }),
    })
  : null

export async function startLogin(): Promise<void> {
  if (!userManager) throw new Error('Keycloak OIDC is not configured.')
  await userManager.signinRedirect()
}

export async function completeLogin(): Promise<AuthSession | null> {
  if (!userManager) return null

  const hasAuthorizationResponse = new URLSearchParams(window.location.search).has('code')
  const user = hasAuthorizationResponse
    ? await userManager.signinCallback()
    : await userManager.getUser()

  if (hasAuthorizationResponse) {
    window.history.replaceState({}, document.title, window.location.pathname)
  }

  return user ? toAuthSession(user) : null
}

export function logout(session: AuthSession | null): void {
  if (!userManager) return
  void userManager.signoutRedirect({ id_token_hint: session?.idToken })
}

function toAuthSession(user: User): AuthSession {
  return {
    accessToken: user.access_token,
    idToken: user.id_token,
    expiresAt: (user.expires_at ?? Math.floor(Date.now() / 1000)) * 1000,
  }
}
