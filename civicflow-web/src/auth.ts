export type AuthSession = {
  accessToken: string
  idToken?: string
  expiresAt: number
}

const authority = import.meta.env.VITE_KEYCLOAK_AUTHORITY as string | undefined
const clientId = import.meta.env.VITE_KEYCLOAK_CLIENT_ID as string | undefined
const redirectUri = import.meta.env.VITE_KEYCLOAK_REDIRECT_URI as string | undefined

export const oidcIsConfigured = Boolean(authority && clientId)
const redirect = redirectUri ?? window.location.origin

function randomString(length: number): string {
  const bytes = new Uint8Array(length)
  crypto.getRandomValues(bytes)
  return Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('')
}

function base64Url(bytes: ArrayBuffer): string {
  return btoa(String.fromCharCode(...new Uint8Array(bytes))).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

async function createCodeChallenge(verifier: string): Promise<string> {
  return base64Url(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier)))
}

export async function startLogin(): Promise<void> {
  if (!authority || !clientId) throw new Error('Keycloak OIDC is not configured.')
  const state = randomString(24)
  const verifier = randomString(48)
  sessionStorage.setItem('civicflow.oidc.state', state)
  sessionStorage.setItem('civicflow.oidc.verifier', verifier)
  const query = new URLSearchParams({ client_id: clientId, redirect_uri: redirect, response_type: 'code', scope: 'openid profile email', state, code_challenge: await createCodeChallenge(verifier), code_challenge_method: 'S256' })
  window.location.assign(`${authority.replace(/\/$/, '')}/protocol/openid-connect/auth?${query}`)
}

export async function completeLogin(): Promise<AuthSession | null> {
  if (!authority || !clientId) return null
  const query = new URLSearchParams(window.location.search)
  const code = query.get('code')
  if (!code) return null
  const expectedState = sessionStorage.getItem('civicflow.oidc.state')
  const actualState = query.get('state')
  const verifier = sessionStorage.getItem('civicflow.oidc.verifier')
  sessionStorage.removeItem('civicflow.oidc.state')
  sessionStorage.removeItem('civicflow.oidc.verifier')
  if (!expectedState || expectedState !== actualState || !verifier) throw new Error('The OIDC state validation failed.')
  const response = await fetch(`${authority.replace(/\/$/, '')}/protocol/openid-connect/token`, { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: new URLSearchParams({ grant_type: 'authorization_code', client_id: clientId, redirect_uri: redirect, code, code_verifier: verifier }) })
  if (!response.ok) throw new Error('Keycloak token exchange failed.')
  const tokens = await response.json() as { access_token: string; id_token?: string; expires_in: number }
  window.history.replaceState({}, document.title, window.location.pathname)
  return { accessToken: tokens.access_token, idToken: tokens.id_token, expiresAt: Date.now() + tokens.expires_in * 1000 }
}

export function logout(session: AuthSession | null): void {
  if (!authority) return
  const query = new URLSearchParams({ client_id: clientId ?? '', post_logout_redirect_uri: redirect })
  if (session?.idToken) query.set('id_token_hint', session.idToken)
  window.location.assign(`${authority.replace(/\/$/, '')}/protocol/openid-connect/logout?${query}`)
}
