import { useEffect, useState } from 'react'
import { Target } from 'lucide-react'
import { NavLink, useParams } from 'react-router'
import { EmptyState, Panel } from '../../components/ui'
import { t } from '../../i18n/es'
import { daysToHorizon, foresightApi, PREDICTIONS_CHANGED, type PredictionFilter, type PredictionPage } from '../../lib/foresight'
import { PredictionStatusBadge } from './badges'
import { PredictionDetail } from './PredictionDetail'

const filters: PredictionFilter[] = ['open', 'overdue', 'soon', 'resolved', 'withdrawn', 'all']

export function PredictionsPage() {
  const { code } = useParams()
  return (
    <div className="grid h-full min-h-0 grid-cols-1 gap-3 p-3 md:grid-cols-[minmax(280px,2fr)_3fr]">
      <Panel title={t.nav.predictions} className={code ? 'hidden md:flex' : ''}>
        <PredictionList />
      </Panel>
      <Panel className={code ? '' : 'hidden md:flex'}>
        {code ? <PredictionDetail code={code} /> : <EmptyState icon={<Target size={24} />} title={t.foresight.select} />}
      </Panel>
    </div>
  )
}

function PredictionList() {
  const [filter, setFilter] = useState<PredictionFilter>('open')
  const [page, setPage] = useState<PredictionPage | null>(null)
  const [version, setVersion] = useState(0)

  useEffect(() => {
    const refresh = () => setVersion((v) => v + 1)
    window.addEventListener(PREDICTIONS_CHANGED, refresh)
    return () => window.removeEventListener(PREDICTIONS_CHANGED, refresh)
  }, [])

  useEffect(() => {
    let alive = true
    foresightApi
      .list(filter)
      .then((p) => alive && setPage(p))
      .catch(() => alive && setPage({ items: [], total: 0 }))
    return () => {
      alive = false
    }
  }, [filter, version])

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="flex shrink-0 flex-wrap gap-1 border-b border-line p-2" role="tablist" aria-label={t.nav.predictions}>
        {filters.map((f) => (
          <button
            key={f}
            type="button"
            role="tab"
            aria-selected={filter === f}
            onClick={() => setFilter(f)}
            className={`rounded-md px-2 py-1 text-xs ${filter === f ? 'bg-accent text-white' : 'text-muted hover:text-ink'}`}
          >
            {t.foresight.filters[f]}
          </button>
        ))}
      </div>
      <ul className="min-h-0 flex-1 overflow-auto">
        {page?.items.length === 0 && (
          <li className="h-full">
            <EmptyState title={t.foresight.empty} />
          </li>
        )}
        {page?.items.map((p) => (
          <li key={p.code}>
            <NavLink
              to={`/predicciones/${p.code}`}
              className={({ isActive }) => `block border-b border-line px-3 py-2 ${isActive ? 'bg-accent-soft' : 'hover:bg-surface-2'}`}
            >
              <div className="flex items-center gap-2">
                <span className="font-mono text-[11px] text-gold">{p.code}</span>
                {p.version > 1 && <span className="font-mono text-[10px] text-muted">v{p.version}</span>}
                <PredictionStatusBadge status={p.status} overdue={p.isOverdue} outcome={p.outcome} />
                <span className="ml-auto text-[11px] text-muted">{p.status === 'Open' ? t.foresight.days(daysToHorizon(p.horizonDate)) : p.horizonDate}</span>
              </div>
              <div className="mt-0.5 line-clamp-2 text-sm">{p.statement}</div>
              <div className="mt-1 flex items-center gap-2 font-mono text-[11px] text-muted">
                <span>{p.signalCode}</span>
                <span className="ml-auto">
                  {p.confidence}% / {p.baseRate}%
                </span>
              </div>
            </NavLink>
          </li>
        ))}
      </ul>
    </div>
  )
}
