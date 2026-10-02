import {
  Activity,
  FileText,
  GitCompareArrows,
  Globe,
  Lightbulb,
  Radar,
  ShieldCheck,
  Target,
  type LucideIcon,
} from 'lucide-react'
import { t } from '../i18n/es'

export type Section = {
  path: string
  label: string
  icon: LucideIcon
  /** Tecla tras "G" para navegar (G S = señales). */
  key: string
  /** Hito de la Fase 3 en que se construye; ausente si ya está disponible. */
  milestone?: string
}

export const sections: Section[] = [
  { path: '/', label: t.nav.radar, icon: Radar, key: 'r' },
  { path: '/senales', label: t.nav.signals, icon: Activity, key: 's', milestone: 'M1' },
  { path: '/predicciones', label: t.nav.predictions, icon: Target, key: 'p', milestone: 'M2' },
  { path: '/eventos', label: t.nav.events, icon: Globe, key: 'e', milestone: 'M3' },
  { path: '/convergencias', label: t.nav.convergences, icon: GitCompareArrows, key: 'c', milestone: 'M4' },
  { path: '/oportunidades', label: t.nav.opportunities, icon: Lightbulb, key: 'o', milestone: 'M5' },
  { path: '/informes', label: t.nav.reports, icon: FileText, key: 'n', milestone: 'Fase 4' },
  { path: '/integridad', label: t.nav.integrity, icon: ShieldCheck, key: 'i' },
]
