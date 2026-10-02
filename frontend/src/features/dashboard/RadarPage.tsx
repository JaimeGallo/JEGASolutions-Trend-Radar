import { useEffect, useState } from 'react'
import { Activity, GitCompareArrows, Globe, Lightbulb, Target } from 'lucide-react'
import { Link } from 'react-router'
import { EmptyState, Kpi, LayerBadge, Panel } from '../../components/ui'
import { t } from '../../i18n/es'
import { fmtRelative } from '../../lib/format'
import { SIGNALS_CHANGED, signalsApi, type SignalPage } from '../../lib/signals'
import { DomainBadge, RetroBadge, StageBadge } from '../signals/badges'

// Los paneles muestran estados vacíos hasta que existan los módulos de la Fase 3.
// Nunca se muestran cifras inventadas: sin datos se dice "Sin datos".
export function RadarPage() {
  const [active, setActive] = useState<SignalPage | null>(null)
  const [version, setVersion] = useState(0)

  useEffect(() => {
    const refresh = () => setVersion((v) => v + 1)
    window.addEventListener(SIGNALS_CHANGED, refresh)
    return () => window.removeEventListener(SIGNALS_CHANGED, refresh)
  }, [])

  useEffect(() => {
    let alive = true
    signalsApi
      .list({ status: 'Active' })
      .then((p) => alive && setActive(p))
      .catch(() => alive && setActive(null))
    return () => {
      alive = false
    }
  }, [version])

  const panels = [
    { title: t.radar.external, icon: Globe, milestone: 'M3' },
    { title: t.radar.convergences, icon: GitCompareArrows, milestone: 'M4' },
    { title: t.radar.predictions, icon: Target, milestone: 'M2' },
    { title: t.radar.opportunities, icon: Lightbulb, milestone: 'M5' },
  ]

  return (
    <div className="flex h-full min-h-0 flex-col gap-3 p-3">
      <div className="grid shrink-0 grid-cols-2 gap-2 sm:grid-cols-3 xl:grid-cols-6">
        <Kpi label={t.kpis.activeSignals} value={active ? active.total : null} />
        <Kpi label={t.kpis.activePredictions} value={null} />
        <Kpi label={t.kpis.beforeConvergences} value={null} />
        <Kpi label={t.kpis.medianWindow} value={null} />
        <Kpi label={t.kpis.failedRate} value={null} />
        <Kpi label={t.kpis.jai} value={null} emptyNote={t.kpis.insufficient(0, 10)} hint={t.kpis.jaiHint} gold />
      </div>
      <div className="grid min-h-0 flex-1 grid-cols-1 gap-3 overflow-auto lg:grid-cols-3 lg:grid-rows-2 lg:overflow-hidden">
        <Panel
          title={t.radar.internal}
          actions={<LayerBadge layer="observation" />}
          className="lg:row-span-2"
          growOnMobile
        >
          {active && active.items.length > 0 ? (
            <ul>
              {active.items.slice(0, 12).map((s) => (
                <li key={s.code}>
                  <Link to={`/senales/${s.code}`} className="block border-b border-line px-3 py-2 hover:bg-surface-2">
                    <div className="flex items-center gap-2">
                      <span className="font-mono text-[11px] text-gold">{s.code}</span>
                      <span className="ml-auto text-[11px] text-muted">{fmtRelative(s.recordedAt)}</span>
                    </div>
                    <div className="mt-0.5 line-clamp-1 text-sm">{s.title}</div>
                    <div className="mt-1 flex gap-1.5">
                      <DomainBadge code={s.subdomainCode ?? s.domainCode} />
                      <StageBadge stage={s.stage} />
                      {s.isRetrospective && <RetroBadge />}
                    </div>
                  </Link>
                </li>
              ))}
              {active.total > 12 && (
                <li className="px-3 py-2 text-xs">
                  <Link to="/senales" className="text-accent hover:underline">
                    {t.signals.count(12, active.total)}
                  </Link>
                </li>
              )}
            </ul>
          ) : (
            <EmptyState icon={<Activity size={22} />} title={t.signals.emptyAll} />
          )}
        </Panel>
        {panels.map((p) => (
          <Panel
            key={p.title}
            title={p.title}
            actions={<LayerBadge layer="observation" />}
            growOnMobile
          >
            <EmptyState icon={<p.icon size={22} />} title={t.sections.pendingTitle} body={t.sections.pendingBody(p.milestone)} />
          </Panel>
        ))}
      </div>
    </div>
  )
}
