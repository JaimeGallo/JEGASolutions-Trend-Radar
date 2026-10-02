import { api } from './api'

export type PredictionStatus = 'Open' | 'Resolved' | 'Withdrawn'
export type PredictionOutcome = 'Confirmed' | 'Partial' | 'Failed' | 'Unresolvable'
export type HypothesisStatus = 'Open' | 'Supported' | 'Weakened' | 'Refuted' | 'Superseded'

export type PredictionSummary = {
  code: string
  signalCode: string
  statement: string
  horizonDate: string
  confidence: number
  baseRate: number
  specificity: number
  status: PredictionStatus
  isOverdue: boolean
  version: number
  outcome: PredictionOutcome | null
  recordedAt: string
}
export type PredictionPage = { items: PredictionSummary[]; total: number }

export type Resolution = {
  id: string
  outcome: PredictionOutcome
  partialCredit: number | null
  rationale: string
  evidenceUrl: string | null
  resolvedAt: string
  resolvedBy: string
  isCurrent: boolean
}

export type PredictionDetail = {
  code: string
  signalCode: string
  signalTitle: string
  hypothesisCode: string | null
  hypothesisStatement: string | null
  statement: string
  resolutionCriteria: string
  horizonDate: string
  confidence: number
  baseRate: number
  specificity: number
  evidenceSnapshot: string | null
  recordedAt: string
  recordedBy: string
  lockedAt: string
  isLocked: boolean
  version: number
  supersedesCode: string | null
  supersededBy: string[]
  status: PredictionStatus
  isOverdue: boolean
  withdrawnAt: string | null
  withdrawalReason: string | null
  resolutions: Resolution[]
  snapshots: { createdAt: string; snapshot: string; chainHash: string }[]
}

export type Hypothesis = {
  code: string
  statement: string
  rationale: string | null
  status: HypothesisStatus
  recordedAt: string
  predictionCodes: string[]
}
export type SignalForesight = { hypotheses: Hypothesis[]; predictions: PredictionSummary[] }

export type PredictionInput = {
  statement: string
  resolutionCriteria: string
  horizonDate: string
  confidence: number
  baseRate: number
  specificity: number
  evidenceSnapshot?: string | null
  hypothesisCode?: string | null
  reason?: string | null
}

export type PredictionFilter = 'open' | 'overdue' | 'soon' | 'resolved' | 'withdrawn' | 'all'

const enc = encodeURIComponent
const filterParams: Record<PredictionFilter, string> = {
  open: 'status=Open',
  overdue: 'due=Overdue',
  soon: 'due=Soon',
  resolved: 'status=Resolved',
  withdrawn: 'status=Withdrawn',
  all: '',
}

export const foresightApi = {
  list: (filter: PredictionFilter) => api<PredictionPage>(`/api/predictions?take=300&${filterParams[filter]}`),
  get: (code: string) => api<PredictionDetail>(`/api/predictions/${enc(code)}`),
  forSignal: (signal: string) => api<SignalForesight>(`/api/signals/${enc(signal)}/foresight`),
  createHypothesis: (signal: string, statement: string, rationale: string | null) =>
    api<SignalForesight>(`/api/signals/${enc(signal)}/hypotheses`, { method: 'POST', body: JSON.stringify({ statement, rationale }) }),
  setHypothesisStatus: (code: string, status: HypothesisStatus, reason: string) =>
    api<void>(`/api/hypotheses/${enc(code)}/status`, { method: 'PUT', body: JSON.stringify({ status, reason }) }),
  create: (signal: string, input: PredictionInput) =>
    api<PredictionDetail>(`/api/signals/${enc(signal)}/predictions`, { method: 'POST', body: JSON.stringify(input) }),
  correct: (code: string, input: PredictionInput) =>
    api<PredictionDetail>(`/api/predictions/${enc(code)}`, { method: 'PUT', body: JSON.stringify(input) }),
  supersede: (code: string, input: PredictionInput) =>
    api<PredictionDetail>(`/api/predictions/${enc(code)}/versions`, { method: 'POST', body: JSON.stringify(input) }),
  resolve: (code: string, input: { outcome: PredictionOutcome; partialCredit: number | null; rationale: string; evidenceUrl: string | null }) =>
    api<PredictionDetail>(`/api/predictions/${enc(code)}/resolution`, { method: 'POST', body: JSON.stringify(input) }),
  withdraw: (code: string, reason: string) =>
    api<PredictionDetail>(`/api/predictions/${enc(code)}/withdraw`, { method: 'POST', body: JSON.stringify({ reason }) }),
}

export const PREDICTIONS_CHANGED = 'tr:predictions-changed'
export function notifyPredictionsChanged() {
  window.dispatchEvent(new Event(PREDICTIONS_CHANGED))
}

/** Días entre hoy (Colombia) y la fecha límite; negativo si ya venció. */
export function daysToHorizon(horizonDate: string) {
  const today = new Date(new Date().toLocaleString('en-US', { timeZone: 'America/Bogota' }))
  today.setHours(0, 0, 0, 0)
  const [y, m, d] = horizonDate.split('-').map(Number)
  return Math.round((new Date(y, m - 1, d).getTime() - today.getTime()) / 86_400_000)
}
