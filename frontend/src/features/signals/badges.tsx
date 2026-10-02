import { History, Paperclip } from 'lucide-react'
import { t } from '../../i18n/es'
import type { EvidenceLevel, SignalStage, SignalStatus } from '../../lib/signals'

const chip = 'inline-flex items-center gap-1 rounded border px-1.5 py-0.5 text-[10px] font-medium whitespace-nowrap'

export function StageBadge({ stage }: { stage: SignalStage }) {
  const cls = { Intuition: 'border-line text-muted', Signal: 'border-accent/50 text-accent', Opportunity: 'border-gold/50 text-gold' }[stage]
  return <span className={`${chip} ${cls}`}>{t.enums.stage[stage]}</span>
}

export function StatusBadge({ status }: { status: SignalStatus }) {
  if (status === 'Active') return null
  return <span className={`${chip} border-line text-muted`}>{t.enums.status[status]}</span>
}

export function RetroBadge() {
  return <span className={`${chip} border-danger/40 text-danger`}>{t.signals.retro}</span>
}

export function DomainBadge({ code }: { code: string }) {
  return <span className={`${chip} border-line bg-surface-2 font-mono text-muted`}>{code}</span>
}

/** Nivel de evidencia de fecha: E0-E1 débil, E2 controlable por el autor, E3 tercero, E4 criptográfico. */
export function LevelBadge({ level }: { level: EvidenceLevel }) {
  const cls = {
    E0: 'border-line text-muted',
    E1: 'border-line text-muted',
    E2: 'border-accent/50 text-accent',
    E3: 'border-ok/50 text-ok',
    E4: 'border-gold/60 bg-gold-soft text-gold',
  }[level]
  return <span className={`${chip} font-mono ${cls}`}>{level}</span>
}

export function Counts({ versions, evidence }: { versions: number; evidence: number }) {
  return (
    <span className="inline-flex items-center gap-2 text-[11px] text-muted">
      <span className="inline-flex items-center gap-0.5" title={t.signals.versions(versions)}>
        <History size={11} aria-hidden /> {versions}
      </span>
      <span className="inline-flex items-center gap-0.5" title={t.signals.evidenceCount(evidence)}>
        <Paperclip size={11} aria-hidden /> {evidence}
      </span>
    </span>
  )
}
