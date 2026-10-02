import { useState, type FormEvent } from 'react'
import { useAuth } from '../../auth/AuthContext'
import { t } from '../../i18n/es'
import { ApiError } from '../../lib/api'

export function LoginPage() {
  const { login } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await login(email, password)
    } catch (err) {
      if (err instanceof ApiError && err.status === 429) setError(t.auth.rateLimited)
      else if (err instanceof ApiError && err.status === 401) setError(err.message)
      else setError(t.auth.genericError)
    } finally {
      setBusy(false)
    }
  }

  const input =
    'w-full rounded-md border border-line bg-surface-2 px-3 py-2 text-ink outline-none focus:border-accent'

  return (
    <div className="flex h-full items-center justify-center overflow-auto p-4">
      <form onSubmit={onSubmit} className="w-full max-w-sm space-y-4 rounded-xl border border-line bg-surface p-6">
        <div className="flex items-center gap-3">
          <img src="/favicon.svg" alt="" className="h-10 w-10" />
          <div>
            <h1 className="text-lg font-semibold">{t.app.name}</h1>
            <p className="text-xs text-gold">{t.app.org}</p>
          </div>
        </div>
        <p className="text-muted">{t.auth.subtitle}</p>
        <label className="block space-y-1">
          <span className="text-xs text-muted">{t.auth.email}</span>
          <input className={input} type="email" autoComplete="username" required value={email} onChange={(e) => setEmail(e.target.value)} />
        </label>
        <label className="block space-y-1">
          <span className="text-xs text-muted">{t.auth.password}</span>
          <input className={input} type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
        </label>
        {error && (
          <p role="alert" className="rounded-md border border-danger/40 px-3 py-2 text-danger">
            {error}
          </p>
        )}
        <button
          type="submit"
          disabled={busy}
          className="w-full rounded-md bg-accent px-3 py-2 font-medium text-white hover:opacity-90 disabled:opacity-60"
        >
          {busy ? t.auth.submitting : t.auth.submit}
        </button>
      </form>
    </div>
  )
}
