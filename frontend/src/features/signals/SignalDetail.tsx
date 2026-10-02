import { useEffect, useState, type ReactNode } from 'react'
import { ChevronDown, Download, FileText, GitCommitHorizontal, Link2, Lock, StickyNote } from 'lucide-react'
import { Link } from 'react-router'
import { useAuth } from '../../auth/AuthContext'
import { Button } from '../../components/form'
import { EmptyState, LayerBadge } from '../../components/ui'
import { t } from '../../i18n/es'
import { apiBlob, ApiError } from '../../lib/api'
import { fmtBytes, fmtDate, fmtDateTime } from '../../lib/format'
import { signalsApi, type Evidence, type SignalDetail as Detail } from '../../lib/signals'
import { DomainBadge, LevelBadge, RetroBadge, StageBadge, StatusBadge } from './badges'
import { SignalForesight } from '../predictions/SignalForesight'
import { ClassifyForm, EvidenceForm, ReviseForm } from './SignalForms'

type Mode = 'revise' | 'classify' | 'evidence' | null

export function SignalDetail({ code }: { code: string }) {
  const [signal, setSignal] = useState<Detail | null>(null)
  const [missing, setMissing] = useState(false)
  const [loadedCode, setLoadedCode] = useState<string | null>(null)
  const [mode, setMode] = useState<Mode>(null)
  const { user } = useAuth()
  const canEdit = user?.role === 'Owner'

  // Al cambiar de señal se limpia el estado durante el render (sin efecto en cascada).
  if (loadedCode !== code) {
    setLoadedCode(code)
    setSignal(null)
    setMissing(false)
    setMode(null)
  }

  useEffect(() => {
    let alive = true
    signalsApi
      .get(code)
      .then((s) => alive && setSignal(s))
      .catch((e) => alive && e instanceof ApiError && e.status === 404 && setMissing(true))
    return () => {
      alive = false
    }
  }, [code])

  if (missing) return <EmptyState title={t.signals.notFound} />
  if (!signal) return null

  const current = signal.versions[signal.versions.length - 1]
  const done = (s: Detail) => {
    setSignal(s)
    setMode(null)
  }

  return (
    <article className="space-y-3 p-3" aria-label={signal.code}>
      <header className="space-y-1.5">
        <Link to="/senales" className="text-xs text-accent md:hidden">
          ← {t.nav.signals}
        </Link>
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-mono text-sm text-gold">{signal.code}</span>
          <DomainBadge code={signal.subdomainCode ?? signal.domainCode} />
          <StageBadge stage={signal.stage} />
          <StatusBadge status={signal.status} />
          {signal.origin.isRetrospective && <RetroBadge />}
        </div>
        <h1 className="text-lg leading-snug font-semibold">{current.title}</h1>
        {canEdit && (
          <div className="flex flex-wrap gap-2 pt-1">
            <Button onClick={() => setMode('revise')}>{t.signals.actions.revise}</Button>
            <Button onClick={() => setMode('evidence')}>{t.signals.actions.addEvidence}</Button>
            <Button onClick={() => setMode('classify')}>{t.signals.actions.classify}</Button>
          </div>
        )}
      </header>

      {mode === 'revise' && <ReviseForm signal={signal} onDone={done} onCancel={() => setMode(null)} />}
      {mode === 'classify' && <ClassifyForm signal={signal} onDone={done} onCancel={() => setMode(null)} />}
      {mode === 'evidence' && <EvidenceForm signal={signal} onDone={done} onCancel={() => setMode(null)} />}

      <Section title={t.signals.sections.original} badge={<LayerBadge layer="observation" />}>
        <div className="space-y-2 rounded-md border border-gold/30 bg-gold-soft/30 p-3">
          <p className="flex items-center gap-1 text-[11px] text-gold">
            <Lock size={11} aria-hidden /> {t.signals.immutable}
          </p>
          <p className="font-medium">{signal.originalTitle}</p>
          <p className="whitespace-pre-wrap">{signal.originalText}</p>
          {signal.originalContext && <p className="whitespace-pre-wrap text-muted">{signal.originalContext}</p>}
        </div>
        <p className="mt-2 text-xs text-muted">{t.signals.recordedBy(fmtDateTime(signal.recordedAt), signal.recordedBy)}</p>
        <p className="text-xs text-muted">
          {t.signals.confidenceAt(signal.confidenceAtCreation)} · {t.enums.sourceType[signal.sourceType]}
        </p>
        {signal.importedFrom && <p className="font-mono text-[11px] text-muted">{t.signals.importedFrom(signal.importedFrom)}</p>}
      </Section>

      <Section title={t.signals.sections.origin} badge={<LayerBadge layer="measured" />}>
        <OriginTable signal={signal} />
      </Section>

      <Section title={`${t.signals.sections.versions} (${signal.versions.length})`} badge={<LayerBadge layer="observation" />}>
        <ol className="space-y-2">
          {[...signal.versions].reverse().map((v) => (
            <li key={v.version} className={`rounded-md border p-2.5 ${v.version === signal.currentVersion ? 'border-accent/50' : 'border-line'}`}>
              <div className="flex flex-wrap items-center gap-2 text-xs">
                <span className="font-mono text-gold">v{v.version}</span>
                {v.version === signal.currentVersion && <span className="text-accent">{t.signals.current}</span>}
                <span className="text-muted">
                  {fmtDateTime(v.createdAt)} · {v.createdBy}
                </span>
                <span className="ml-auto font-mono text-[10px] text-muted" title={v.chainHash}>
                  {v.chainHash.slice(0, 12)}…
                </span>
              </div>
              <p className="mt-1 text-xs text-muted italic">{v.changeReason}</p>
              {v.version > 1 && (
                <>
                  <p className="mt-1 font-medium">{v.title}</p>
                  <p className="text-sm whitespace-pre-wrap">{v.text}</p>
                  {v.confidence !== null && <p className="text-xs text-muted">{t.signals.versionConfidence(v.confidence)}</p>}
                </>
              )}
            </li>
          ))}
        </ol>
      </Section>

      <Section title={`${t.signals.sections.evidence} (${signal.evidence.length})`} badge={<LayerBadge layer="observation" />}>
        {signal.evidence.length === 0 ? (
          <p className="text-muted">{t.signals.noEvidence}</p>
        ) : (
          <ul className="space-y-2">
            {signal.evidence.map((e) => (
              <EvidenceItem key={e.id} evidence={e} supporting={e.id === signal.origin.supportingEvidenceId} />
            ))}
          </ul>
        )}
      </Section>

      <Section title={t.foresight.section} badge={<LayerBadge layer="interpretation" />}>
        <SignalForesight signalCode={signal.code} canEdit={canEdit} />
      </Section>

      <Section title={t.signals.sections.classification} badge={<LayerBadge layer="interpretation" />}>
        <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
          <Row label={t.signals.domain} value={`${signal.domainName}${signal.subdomainName ? ` · ${signal.subdomainName}` : ''}`} />
          <Row label={t.signals.stage} value={t.enums.stage[signal.stage]} />
          <Row label={t.signals.status} value={t.enums.status[signal.status]} />
          <Row label={t.signals.impact} value={signal.impactEstimate} />
          <Row label={t.signals.relevance} value={signal.relevanceToJegas} />
          <Row label={t.signals.geographicScope} value={signal.geographicScope} />
          <Row label={t.signals.confidentiality} value={t.enums.confidentiality[signal.confidentiality]} />
          <Row label={t.signals.sourceReference} value={signal.sourceReference} />
        </dl>
      </Section>
    </article>
  )
}

function Section({ title, badge, children }: { title: string; badge?: ReactNode; children: ReactNode }) {
  return (
    <details open className="group rounded-lg border border-line bg-surface">
      <summary className="flex cursor-pointer list-none items-center gap-2 px-3 py-2 text-xs font-semibold tracking-wide text-muted uppercase">
        <ChevronDown size={14} className="-rotate-90 transition-transform group-open:rotate-0" aria-hidden />
        <span className="flex-1">{title}</span>
        {badge}
      </summary>
      <div className="border-t border-line p-3">{children}</div>
    </details>
  )
}

function Row({ label, value }: { label: string; value: ReactNode }) {
  return (
    <>
      <dt className="text-muted">{label}</dt>
      <dd>{value ?? <span className="text-muted">·</span>}</dd>
    </>
  )
}

function OriginTable({ signal }: { signal: Detail }) {
  const { claimed, basis, supportedOriginAt, supportedLevel } = signal.origin
  const claimedText = claimed.earliest
    ? `${claimed.note ?? ''} (${fmtDate(claimed.earliest)} a ${fmtDate(claimed.latest!)}, precisión: ${t.enums.precision[claimed.precision!]})`
    : (claimed.note ?? t.signals.originRows.none)
  return (
    <div className="space-y-2">
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
        <Row label={t.signals.originRows.recorded} value={<span className="font-mono text-xs">{fmtDateTime(signal.recordedAt)}</span>} />
        <Row label={t.signals.originRows.claimed} value={claimedText} />
        <Row
          label={t.signals.originRows.supported}
          value={
            supportedOriginAt ? (
              <span className="inline-flex items-center gap-2">
                <span className="font-mono text-xs">{fmtDateTime(supportedOriginAt)}</span>
                {supportedLevel && <LevelBadge level={supportedLevel} />}
              </span>
            ) : null
          }
        />
      </dl>
      <p className={`rounded-md border px-3 py-2 text-xs ${basis === 'ClaimedOnly' ? 'border-danger/40 text-danger' : 'border-line text-muted'}`}>
        {t.signals.basis[basis]}
      </p>
    </div>
  )
}

const kindIcon = { Url: Link2, GitCommit: GitCommitHorizontal, File: FileText, Note: StickyNote }

function EvidenceItem({ evidence: e, supporting }: { evidence: Evidence; supporting: boolean }) {
  const Icon = kindIcon[e.kind]
  const download = async () => {
    const blob = await apiBlob(`/api/evidence/${e.id}/file`)
    const href = URL.createObjectURL(blob)
    const a = Object.assign(document.createElement('a'), { href, download: e.fileName ?? e.fileSha256 ?? 'evidencia' })
    a.click()
    URL.revokeObjectURL(href)
  }
  return (
    <li className={`rounded-md border p-2.5 ${supporting ? 'border-ok/50' : 'border-line'}`}>
      <div className="flex flex-wrap items-center gap-2 text-xs">
        <Icon size={14} className="text-muted" aria-hidden />
        <span className="text-muted">{t.enums.role[e.role]}</span>
        <LevelBadge level={e.level} />
        {e.artifactTimestamp && <span className="font-mono text-muted">{fmtDateTime(e.artifactTimestamp)}</span>}
        <span className="ml-auto text-[11px] text-muted">{t.enums.authority[e.timestampAuthority]}</span>
      </div>
      <p className="mt-1 text-sm">{e.description}</p>
      {e.url && (
        <a href={e.url} target="_blank" rel="noreferrer noopener" className="block truncate text-xs text-accent underline">
          {e.url}
        </a>
      )}
      {e.gitCommit && (
        <p className="font-mono text-xs text-muted">
          {e.gitRepo} @ {e.gitCommit.slice(0, 12)}
        </p>
      )}
      {e.fileSha256 && (
        <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-muted">
          <span>{e.fileName}</span>
          {e.fileSize !== null && <span>{fmtBytes(e.fileSize)}</span>}
          <span className="font-mono text-[10px]" title={e.fileSha256}>
            sha256 {e.fileSha256.slice(0, 12)}…
          </span>
          <button type="button" onClick={download} className="inline-flex items-center gap-1 text-accent hover:underline">
            <Download size={12} aria-hidden /> {t.signals.evidence.download}
          </button>
        </div>
      )}
    </li>
  )
}
