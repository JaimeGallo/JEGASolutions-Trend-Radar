import { useEffect, useState } from 'react'
import { Search } from 'lucide-react'
import { NavLink } from 'react-router'
import { EmptyState } from '../../components/ui'
import { t } from '../../i18n/es'
import { fmtRelative } from '../../lib/format'
import { SIGNALS_CHANGED, signalsApi, type SignalFilters, type SignalPage } from '../../lib/signals'
import { useDomains } from '../../lib/useDomains'
import { Counts, DomainBadge, RetroBadge, StageBadge, StatusBadge } from './badges'

const filterClass = 'rounded border border-line bg-surface-2 px-1.5 py-1 text-xs text-ink outline-none focus:border-accent'

export function SignalList() {
  const { topLevel } = useDomains()
  const [filters, setFilters] = useState<SignalFilters>({})
  const [query, setQuery] = useState('')
  const [page, setPage] = useState<SignalPage | null>(null)
  const [version, setVersion] = useState(0)

  // Búsqueda con pequeño retardo para no consultar en cada tecla.
  useEffect(() => {
    const id = window.setTimeout(() => setFilters((f) => ({ ...f, q: query.trim() || undefined })), 250)
    return () => window.clearTimeout(id)
  }, [query])

  useEffect(() => {
    const refresh = () => setVersion((v) => v + 1)
    window.addEventListener(SIGNALS_CHANGED, refresh)
    return () => window.removeEventListener(SIGNALS_CHANGED, refresh)
  }, [])

  useEffect(() => {
    let alive = true
    signalsApi
      .list(filters)
      .then((p) => alive && setPage(p))
      .catch(() => alive && setPage({ items: [], total: 0 }))
    return () => {
      alive = false
    }
  }, [filters, version])

  const set = (key: keyof SignalFilters) => (e: React.ChangeEvent<HTMLSelectElement>) =>
    setFilters((f) => ({ ...f, [key]: e.target.value || undefined }))
  const filtered = Object.entries(filters).some(([, v]) => v)

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="shrink-0 space-y-2 border-b border-line p-2">
        <label className="flex items-center gap-2 rounded-md border border-line bg-surface-2 px-2 py-1">
          <Search size={14} className="text-muted" aria-hidden />
          <input
            data-search
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={t.signals.searchPlaceholder}
            aria-label={t.signals.searchPlaceholder}
            className="w-full bg-transparent text-sm outline-none placeholder:text-muted/70"
          />
        </label>
        <div className="flex flex-wrap gap-1.5">
          <select aria-label={t.signals.domain} className={filterClass} value={filters.domain ?? ''} onChange={set('domain')}>
            <option value="">{t.signals.allDomains}</option>
            {topLevel.map((d) => (
              <option key={d.code} value={d.code}>
                {d.name}
              </option>
            ))}
          </select>
          <select aria-label={t.signals.stage} className={filterClass} value={filters.stage ?? ''} onChange={set('stage')}>
            <option value="">{t.signals.allStages}</option>
            {Object.entries(t.enums.stage).map(([v, l]) => (
              <option key={v} value={v}>
                {l}
              </option>
            ))}
          </select>
          <select aria-label={t.signals.status} className={filterClass} value={filters.status ?? ''} onChange={set('status')}>
            <option value="">{t.signals.allStatuses}</option>
            {Object.entries(t.enums.status).map(([v, l]) => (
              <option key={v} value={v}>
                {l}
              </option>
            ))}
          </select>
        </div>
        {page && <p className="text-[11px] text-muted">{t.signals.count(page.items.length, page.total)}</p>}
      </div>
      <ul className="min-h-0 flex-1 overflow-auto" aria-label={t.nav.signals}>
        {page?.items.length === 0 && (
          <li className="h-full">
            <EmptyState title={filtered ? t.signals.empty : t.signals.emptyAll} />
          </li>
        )}
        {page?.items.map((s) => (
          <li key={s.code}>
            <NavLink
              to={`/senales/${s.code}`}
              className={({ isActive }) =>
                `block border-b border-line px-3 py-2 ${isActive ? 'bg-accent-soft' : 'hover:bg-surface-2'}`
              }
            >
              <div className="flex items-center gap-2">
                <span className="font-mono text-[11px] text-gold">{s.code}</span>
                <span className="ml-auto text-[11px] text-muted">{fmtRelative(s.recordedAt)}</span>
              </div>
              <div className="mt-0.5 line-clamp-2 text-sm">{s.title}</div>
              <div className="mt-1 flex flex-wrap items-center gap-1.5">
                <DomainBadge code={s.subdomainCode ?? s.domainCode} />
                <StageBadge stage={s.stage} />
                <StatusBadge status={s.status} />
                {s.isRetrospective && <RetroBadge />}
                <span className="ml-auto">
                  <Counts versions={s.currentVersion} evidence={s.evidenceCount} />
                </span>
              </div>
            </NavLink>
          </li>
        ))}
      </ul>
    </div>
  )
}
