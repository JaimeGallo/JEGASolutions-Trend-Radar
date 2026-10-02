import type { ReactNode } from 'react'
import { Lock, Ruler, Sparkles } from 'lucide-react'
import { t } from '../i18n/es'

export function Panel({
  title,
  actions,
  children,
  className = '',
  growOnMobile = false,
}: {
  title?: ReactNode
  actions?: ReactNode
  children: ReactNode
  className?: string
  /** En pantallas pequeñas el panel crece con su contenido y desplaza la página. */
  growOnMobile?: boolean
}) {
  return (
    <section
      className={`flex flex-col rounded-lg border border-line bg-surface ${growOnMobile ? 'lg:min-h-0' : 'min-h-0'} ${className}`}
    >
      {(title || actions) && (
        <header className="flex shrink-0 items-center justify-between gap-2 border-b border-line px-3 py-2">
          <h2 className="text-xs font-semibold tracking-wide text-muted uppercase">{title}</h2>
          {actions}
        </header>
      )}
      <div className={growOnMobile ? 'lg:min-h-0 lg:flex-1 lg:overflow-auto' : 'min-h-0 flex-1 overflow-auto'}>
        {children}
      </div>
    </section>
  )
}

export function Kpi({
  label,
  value,
  emptyNote = t.kpis.noData,
  hint,
  gold,
}: {
  label: string
  /** null = sin datos todavía; nunca se rellena con cifras inventadas. */
  value: ReactNode | null
  emptyNote?: string
  hint?: string
  gold?: boolean
}) {
  return (
    <div className="min-w-0 rounded-lg border border-line bg-surface px-3 py-2" title={hint}>
      <div className="truncate text-[11px] tracking-wide text-muted uppercase">{label}</div>
      {value === null ? (
        <div className="mt-1 truncate text-sm text-muted">{emptyNote}</div>
      ) : (
        <div className={`mt-0.5 truncate font-mono text-lg ${gold ? 'text-gold' : 'text-ink'}`}>{value}</div>
      )}
    </div>
  )
}

/** Marca de capa epistémica (PRODUCT_SPEC §8): observación, interpretación o resultado medido. */
export function LayerBadge({ layer }: { layer: 'observation' | 'interpretation' | 'measured' }) {
  const styles = {
    observation: { icon: Lock, cls: 'border-gold/40 bg-gold-soft text-gold' },
    interpretation: { icon: Sparkles, cls: 'border-accent/40 bg-accent-soft text-accent' },
    measured: { icon: Ruler, cls: 'border-ok/40 text-ok' },
  }[layer]
  const Icon = styles.icon
  return (
    <span className={`inline-flex items-center gap-1 rounded border px-1.5 py-0.5 text-[10px] font-semibold uppercase ${styles.cls}`}>
      <Icon size={11} aria-hidden />
      {t.layers[layer]}
    </span>
  )
}

export function EmptyState({
  icon,
  title,
  body,
  children,
}: {
  icon?: ReactNode
  title: string
  body?: string
  children?: ReactNode
}) {
  return (
    <div className="flex h-full flex-col items-center justify-center gap-2 p-6 text-center">
      {icon && <div className="text-muted">{icon}</div>}
      <p className="font-medium text-ink">{title}</p>
      {body && <p className="max-w-sm text-muted">{body}</p>}
      {children && <div className="max-w-sm text-muted">{children}</div>}
    </div>
  )
}
