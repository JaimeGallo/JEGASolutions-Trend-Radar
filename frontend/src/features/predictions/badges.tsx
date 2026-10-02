import { t } from '../../i18n/es'
import type { PredictionOutcome, PredictionStatus } from '../../lib/foresight'

const chip = 'inline-flex items-center rounded border px-1.5 py-0.5 text-[10px] font-medium whitespace-nowrap'

export function PredictionStatusBadge({
  status,
  overdue,
  outcome,
}: {
  status: PredictionStatus
  overdue: boolean
  outcome: PredictionOutcome | null
}) {
  if (status === 'Resolved' && outcome) {
    const cls = {
      Confirmed: 'border-ok/50 text-ok',
      Partial: 'border-gold/50 text-gold',
      Failed: 'border-danger/50 text-danger',
      Unresolvable: 'border-line text-muted',
    }[outcome]
    return <span className={`${chip} ${cls}`}>{t.foresight.outcome[outcome]}</span>
  }
  if (status === 'Withdrawn') return <span className={`${chip} border-line text-muted line-through`}>{t.foresight.status.Withdrawn}</span>
  if (overdue) return <span className={`${chip} border-danger/60 bg-danger/10 text-danger`}>{t.foresight.status.Overdue}</span>
  return <span className={`${chip} border-accent/50 text-accent`}>{t.foresight.status.Open}</span>
}

/** Barra doble: confianza declarada frente a la tasa base. La diferencia es la apuesta. */
export function ConfidenceBars({ confidence, baseRate }: { confidence: number; baseRate: number }) {
  return (
    <div className="space-y-1 text-[11px]" aria-label={t.foresight.vsBase}>
      {[
        { label: t.foresight.confidence.split(' (')[0], value: confidence, cls: 'bg-accent' },
        { label: t.foresight.baseRate.split(' (')[0], value: baseRate, cls: 'bg-muted/60' },
      ].map((b) => (
        <div key={b.label} className="grid grid-cols-[7rem_1fr_2.5rem] items-center gap-2">
          <span className="text-muted">{b.label}</span>
          <span className="h-1.5 rounded bg-surface-2">
            <span className={`block h-1.5 rounded ${b.cls}`} style={{ width: `${b.value}%` }} />
          </span>
          <span className="text-right font-mono">{b.value}%</span>
        </div>
      ))}
    </div>
  )
}
