import { Search } from 'lucide-react'
import type { Section } from '../../app/sections'
import { EmptyState, Panel } from '../../components/ui'
import { t } from '../../i18n/es'

/**
 * Esqueleto maestro-detalle de cada sección (PRODUCT_SPEC §8): lista filtrable a la
 * izquierda y detalle contextual a la derecha, sin cambiar de página.
 */
export function SectionPage({ section }: { section: Section }) {
  return (
    <div className="flex h-full min-h-0 flex-col gap-3 p-3">
      <header className="flex shrink-0 items-center gap-2">
        <section.icon size={18} className="text-accent" aria-hidden />
        <h1 className="text-base font-semibold">{section.label}</h1>
        {section.milestone && (
          <span className="rounded border border-line px-1.5 py-0.5 text-[11px] text-muted">{section.milestone}</span>
        )}
      </header>
      <div className="grid min-h-0 flex-1 grid-cols-1 gap-3 md:grid-cols-[minmax(260px,1fr)_2fr]">
        <Panel
          title={section.label}
          actions={
            <label className="flex items-center gap-1 rounded border border-line px-2 py-0.5 text-muted">
              <Search size={12} aria-hidden />
              <input disabled placeholder={t.sections.filterPlaceholder} className="w-24 bg-transparent text-xs outline-none" />
            </label>
          }
        >
          <EmptyState title={t.sections.emptyList} />
        </Panel>
        <Panel title="Detalle">
          <EmptyState
            icon={<section.icon size={24} />}
            title={t.sections.pendingTitle}
            body={section.milestone ? t.sections.pendingBody(section.milestone) : t.sections.selectHint}
          />
        </Panel>
      </div>
    </div>
  )
}
