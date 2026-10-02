import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { authApi, refreshSession, setAccessToken, setSessionLostHandler, type User } from '../lib/api'

type AuthState = {
  user: User | null
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    // Al cargar, intenta recuperar la sesión con la cookie de renovación.
    let cancelled = false
    refreshSession()
      .then((auth) => !cancelled && setUser(auth?.user ?? null))
      .finally(() => !cancelled && setLoading(false))
    setSessionLostHandler(() => setUser(null))
    return () => {
      cancelled = true
      setSessionLostHandler(null)
    }
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const auth = await authApi.login(email, password)
    setAccessToken(auth.accessToken)
    setUser(auth.user)
  }, [])

  const logout = useCallback(async () => {
    try {
      await authApi.logout()
    } finally {
      setAccessToken(null)
      setUser(null)
    }
  }, [])

  const value = useMemo(() => ({ user, loading, login, logout }), [user, loading, login, logout])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth debe usarse dentro de AuthProvider')
  return ctx
}
