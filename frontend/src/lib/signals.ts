import { api } from './api'

export type SignalStage = 'Intuition' | 'Signal' | 'Opportunity'
export type SignalStatus = 'Active' | 'Dormant' | 'Archived'
export type SourceType =
  | 'Observation'
  | 'ProjectDecision'
  | 'ClientInteraction'
  | 'Prototype'
  | 'Commit'
  | 'Document'
  | 'Conversation'
  | 'MarketObservation'
  | 'RejectedIdea'
  | 'Experiment'
export type Confidentiality = 'Public' | 'Internal' | 'ClientConfidential'
export type EvidenceKind = 'File' | 'Url' | 'GitCommit' | 'Note'
export type EvidenceRole = 'Origin' | 'Supports' | 'Contradicts'
export type TimestampAuthority =
  | 'None'
  | 'Self'
  | 'GitAuthor'
  | 'FileMetadata'
  | 'GitHub'
  | 'EmailProvider'
  | 'ChatProvider'
  | 'Wayback'
  | 'Hosting'
  | 'OpenTimestamps'
  | 'Rfc3161'
export type EvidenceLevel = 'E0' | 'E1' | 'E2' | 'E3' | 'E4'
export type OriginBasis = 'PreRegistered' | 'VerifiedOrigin' | 'ClaimedOnly'
export type DatePrecision = 'Day' | 'Month' | 'Quarter' | 'Year' | 'Approximate'

export type SignalSummary = {
  code: string
  title: string
  domainCode: string
  subdomainCode: string | null
  stage: SignalStage
  status: SignalStatus
  recordedAt: string
  isRetrospective: boolean
  currentVersion: number
  evidenceCount: number
  confidence: number | null
}
export type SignalPage = { items: SignalSummary[]; total: number }

export type SignalVersion = {
  version: number
  title: string
  text: string
  context: string | null
  confidence: number | null
  changeReason: string
  createdAt: string
  createdBy: string
  chainHash: string
}

export type Evidence = {
  id: string
  kind: EvidenceKind
  role: EvidenceRole
  description: string
  url: string | null
  gitRepo: string | null
  gitCommit: string | null
  fileName: string | null
  fileSha256: string | null
  fileMime: string | null
  fileSize: number | null
  artifactTimestamp: string | null
  timestampAuthority: TimestampAuthority
  level: EvidenceLevel
  recordedAt: string
}

export type SignalDetail = {
  code: string
  originalTitle: string
  originalText: string
  originalContext: string | null
  recordedAt: string
  recordedBy: string
  recordedTimeZone: string
  confidenceAtCreation: number | null
  sourceType: SourceType
  importedFrom: string | null
  domainCode: string
  domainName: string
  subdomainCode: string | null
  subdomainName: string | null
  stage: SignalStage
  status: SignalStatus
  sourceReference: string | null
  geographicScope: string | null
  impactEstimate: number | null
  relevanceToJegas: number | null
  confidentiality: Confidentiality
  currentVersion: number
  origin: {
    recordedAt: string
    isRetrospective: boolean
    claimed: { earliest: string | null; latest: string | null; precision: DatePrecision | null; note: string | null }
    basis: OriginBasis
    supportedOriginAt: string | null
    supportedLevel: EvidenceLevel | null
    supportingEvidenceId: string | null
  }
  versions: SignalVersion[]
  evidence: Evidence[]
}

export type CaptureInput = {
  title: string
  text: string
  domainCode: string
  context?: string | null
  subdomainCode?: string | null
  stage?: SignalStage
  confidence?: number | null
  sourceType?: SourceType
  claimedOrigin?: string | null
  timeZone?: string
}

export type ClassificationInput = {
  stage: SignalStage
  status: SignalStatus
  subdomainCode: string | null
  sourceReference: string | null
  geographicScope: string | null
  impactEstimate: number | null
  relevanceToJegas: number | null
  confidentiality: Confidentiality
  reason: string
}

export type EvidenceInput = {
  kind: Exclude<EvidenceKind, 'File'>
  role: EvidenceRole
  description: string
  url?: string | null
  gitRepo?: string | null
  gitCommit?: string | null
  artifactTimestamp?: string | null
  timestampAuthority: TimestampAuthority
}

export type SignalFilters = { q?: string; domain?: string; stage?: string; status?: string }

const enc = encodeURIComponent

export const signalsApi = {
  list: (f: SignalFilters) => {
    const p = new URLSearchParams({ take: '200' })
    Object.entries(f).forEach(([k, v]) => v && p.set(k, v))
    return api<SignalPage>(`/api/signals?${p}`)
  },
  get: (code: string) => api<SignalDetail>(`/api/signals/${enc(code)}`),
  capture: (input: CaptureInput) =>
    api<SignalDetail>('/api/signals', {
      method: 'POST',
      body: JSON.stringify({ timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone, ...input }),
    }),
  revise: (code: string, input: { title: string; text: string; context: string | null; confidence: number | null; reason: string }) =>
    api<SignalDetail>(`/api/signals/${enc(code)}/versions`, { method: 'POST', body: JSON.stringify(input) }),
  reclassify: (code: string, input: ClassificationInput) =>
    api<SignalDetail>(`/api/signals/${enc(code)}/classification`, { method: 'PUT', body: JSON.stringify(input) }),
  addEvidence: (code: string, input: EvidenceInput) =>
    api<SignalDetail>(`/api/signals/${enc(code)}/evidence`, { method: 'POST', body: JSON.stringify(input) }),
  addFile: (
    code: string,
    file: File,
    meta: { role: EvidenceRole; description: string; artifactTimestamp: string | null; timestampAuthority: TimestampAuthority },
  ) => {
    const form = new FormData()
    form.set('file', file)
    form.set('role', meta.role)
    form.set('description', meta.description)
    form.set('timestampAuthority', meta.timestampAuthority)
    if (meta.artifactTimestamp) form.set('artifactTimestamp', meta.artifactTimestamp)
    return api<SignalDetail>(`/api/signals/${enc(code)}/evidence/file`, { method: 'POST', body: form })
  },
}

/** Aviso global para refrescar listas y KPIs cuando cambia una señal. */
export const SIGNALS_CHANGED = 'tr:signals-changed'
export function notifySignalsChanged() {
  window.dispatchEvent(new Event(SIGNALS_CHANGED))
}
