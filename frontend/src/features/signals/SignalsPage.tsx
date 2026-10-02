import { Activity } from 'lucide-react'
import { useParams } from 'react-router'
import { EmptyState, Panel } from '../../components/ui'
import { t } from '../../i18n/es'
import { SignalDetail } from './SignalDetail'
import { SignalList } from './SignalList'

/** Maestro-detalle: la lista y la señal abierta conviven sin cambiar de página (PRODUCT_SPEC §8). */
export function SignalsPage() {
  const { code } = useParams()
  return (
    <div className="grid h-full min-h-0 grid-cols-1 gap-3 p-3 md:grid-cols-[minmax(280px,2fr)_3fr]">
      <Panel title={t.nav.signals} className={code ? 'hidden md:flex' : ''}>
        <SignalList />
      </Panel>
      <Panel className={code ? '' : 'hidden md:flex'}>
        {code ? <SignalDetail code={code} /> : <EmptyState icon={<Activity size={24} />} title={t.signals.select} />}
      </Panel>
    </div>
  )
}
