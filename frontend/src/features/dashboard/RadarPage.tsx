import { useEffect, useState } from 'react'
import { Activity, GitCompareArrows, Globe, Lightbulb, Target } from 'lucide-react'
import { EmptyState, Kpi, LayerBadge, Panel } from '../../components/ui'
import { t } from '../../i18n/es'
import { api, type SignalDomain } from '../../lib/api'

// Los paneles muestran estados vacíos hasta que existan los módulos de la Fase 3.
// Nunca se muestran cifras inventadas: sin datos se dice "Sin datos".
export function RadarPage() {
  const [domains, setDomains] = useState<SignalDomain[] | null>(null)

  useEffect(() => {
    api<SignalDomain[]>('/api/domains').then(setDomains).catch(() => setDomains([]))
  }, [])

  const panels = [
    { title: t.radar.internal, icon: Activity, milestone: 'M1', className: 'lg:row-span-2' },
    { title: t.radar.external, icon: Globe, milestone: 'M3' },
    { title: t.radar.convergences, icon: GitCompareArrows, milestone: 'M4' },
    { title: t.radar.predictions, icon: Target, milestone: 'M2' },
    { title: t.radar.opportunities, icon: Lightbulb, milestone: 'M5' },
  ]

  return (
    <div className="flex h-full min-h-0 flex-col gap-3 p-3">
      <div className="grid shrink-0 grid-cols-2 gap-2 sm:grid-cols-3 xl:grid-cols-6">
        <Kpi label={t.kpis.activeSignals} value={null} />
        <Kpi label={t.kpis.activePredictions} value={null} />
        <Kpi label={t.kpis.beforeConvergences} value={null} />
        <Kpi label={t.kpis.medianWindow} value={null} />
        <Kpi label={t.kpis.failedRate} value={null} />
        <Kpi label={t.kpis.jai} value={null} emptyNote={t.kpis.insufficient(0, 10)} hint={t.kpis.jaiHint} gold />
      </div>
      <div className="grid min-h-0 flex-1 grid-cols-1 gap-3 overflow-auto lg:grid-cols-3 lg:grid-rows-2 lg:overflow-hidden">
        {panels.map((p) => (
          <Panel
            key={p.title}
            title={p.title}
            actions={<LayerBadge layer="observation" />}
            className={p.className ?? ''}
            growOnMobile
          >
            <EmptyState icon={<p.icon size={22} />} title={t.sections.pendingTitle} body={t.sections.pendingBody(p.milestone)}>
              {p.milestone === 'M1' && (
                <div className="mt-4 space-y-1 border-t border-line pt-3 text-xs">
                  <p>{t.radar.phase0}</p>
                  {domains && <p className="font-mono">{t.radar.domains(domains.length)}</p>}
                </div>
              )}
            </EmptyState>
          </Panel>
        ))}
      </div>
    </div>
  )
}
