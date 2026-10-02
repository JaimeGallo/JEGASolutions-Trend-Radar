// Cliente HTTP mínimo. El token de acceso vive solo en memoria; el de renovación
// en una cookie HttpOnly que el navegador envía a /api/auth.

export type User = { id: string; email: string; displayName: string; role: string; isActive: boolean }
export type AuthResponse = { accessToken: string; expiresAt: string; user: User }
export type SignalDomain = { id: string; code: string; name: string; parentId: string | null }
export type AuditEntry = {
  id: number
  occurredAt: string
  userId: string | null
  action: string
  entityType: string
  entityId: string | null
  oldValue: string | null
  newValue: string | null
  reason: string | null
  chainHash: string
}
export type IntegrityReport = {
  isValid: boolean
  checkedEntries: number
  firstInvalidEntryId: number | null
  verifiedAt: string
}

export class ApiError extends Error {
  readonly status: number
  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

let accessToken: string | null = null
let onSessionLost: (() => void) | null = null
let refreshing: Promise<AuthResponse | null> | null = null

export function setAccessToken(token: string | null) {
  accessToken = token
}

export function setSessionLostHandler(handler: (() => void) | null) {
  onSessionLost = handler
}

async function parseError(res: Response): Promise<ApiError> {
  try {
    const body = (await res.json()) as { title?: string }
    return new ApiError(res.status, body.title ?? res.statusText)
  } catch {
    return new ApiError(res.status, res.statusText)
  }
}

export async function refreshSession(): Promise<AuthResponse | null> {
  // Una sola renovación a la vez: el token rota y reusarlo cierra todas las sesiones.
  refreshing ??= (async () => {
    try {
      const res = await fetch('/api/auth/refresh', { method: 'POST', credentials: 'same-origin' })
      if (!res.ok) return null
      const auth = (await res.json()) as AuthResponse
      setAccessToken(auth.accessToken)
      return auth
    } finally {
      refreshing = null
    }
  })()
  return refreshing
}

export async function api<T>(path: string, init: RequestInit = {}, retry = true): Promise<T> {
  const headers = new Headers(init.headers)
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json')

  const res = await fetch(path, { ...init, headers, credentials: 'same-origin' })
  if (res.status === 401 && retry && !path.startsWith('/api/auth/')) {
    if (await refreshSession()) return api<T>(path, init, false)
    setAccessToken(null)
    onSessionLost?.()
  }
  if (!res.ok) throw await parseError(res)
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}

export const authApi = {
  login: (email: string, password: string) =>
    api<AuthResponse>('/api/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }),
  logout: () => api<void>('/api/auth/logout', { method: 'POST' }),
}
