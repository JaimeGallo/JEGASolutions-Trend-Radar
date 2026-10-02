import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { ChevronDown, Lock, Unlock } from 'lucide-react'
import { Link, useNavigate } from 'react-router'
import { useAuth } from '../../auth/AuthContext'
import { Button, Field, FormError, Select, TextArea, TextInput } from '../../components/form'
import { EmptyState, LayerBadge } from '../../components/ui'
import { t } from '../../i18n/es'
import { ApiError } from '../../lib/api'
import { foresightApi, daysToHorizon, notifyPredictionsChanged, type PredictionDetail as Detail, type PredictionOutcome } from '../../lib/foresight'
import { fmtDate, fmtDateTime } from '../../lib/format'
import { ConfidenceBars, PredictionStatusBadge } from './badges'
import { PredictionForm } from './PredictionForm'

type Mode = 'correct' | 'supersede' | 'resolve' | 'withdraw' | null

const timeFmt = new Intl.DateTimeFormat('es-CO', { timeStyle: 'short', timeZone: 'America/Bogota' })

export function PredictionDetail({ code }: { code: string }) {
  const [p, setP] = useState<Detail | null>(null)
  const [missing, setMissing] = useState(false)
  const [loadedCode, setLoadedCode] = useState<string | null>(null)
  const [mode, setMode] = useState<Mode>(null)
  const [now, setNow] = useState(() => Date.now())
  const navigate = useNavigate()
  const { user } = useAuth()
  const canEdit = user?.role === 'Owner'

  if (loadedCode !== code) {
    setLoadedCode(code)
    setP(null)
    setMissing(false)
    setMode(null)
  }

  useEffect(() => {
    let alive = true
    foresightApi
      .get(code)
      .then((d) => alive && setP(d))
      .catch((e) => alive && e instanceof ApiError && e.status === 404 && setMissing(true))
    return () => {
      alive = false
    }
  }, [code])

  // Reloj para la cuenta regresiva del período de gracia.
  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), 15_000)
    return () => window.clearInterval(id)
  }, [])

  if (missing) return <EmptyState title={t.foresight.notFound} />
  if (!p) return null

  // Fuera de la gracia o ya cerrada (resuelta o retirada), el contenido no se puede tocar.
  const locked = p.isLocked || now >= new Date(p.lockedAt).getTime() || p.status !== 'Open'
  const minutesLeft = Math.max(0, Math.ceil((new Date(p.lockedAt).getTime() - now) / 60_000))
  const current = p.resolutions.find((r) => r.isCurrent)
  const days = daysToHorizon(p.horizonDate)
  const done = (d: Detail) => {
    setMode(null)
    if (d.code !== p.code) navigate(`/predicciones/${d.code}`)
    else setP(d)
  }

  return (
    <article className="space-y-3 p-3" aria-label={p.code}>
      <header className="space-y-1.5">
        <Link to="/predicciones" className="text-xs text-accent md:hidden">
          ← {t.nav.predictions}
        </Link>
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-mono text-sm text-gold">{p.code}</span>
          {p.version > 1 && <span className="font-mono text-[11px] text-muted">v{p.version}</span>}
          <PredictionStatusBadge status={p.status} overdue={p.isOverdue} outcome={current?.outcome ?? null} />
          <span className="text-xs text-muted">
            {t.foresight.horizon}: {fmtDate(`${p.horizonDate}T12:00:00-05:00`)}
            {p.status === 'Open' && ` · ${t.foresight.days(days)}`}
          </span>
        </div>
        <p className="text-xs text-muted">
          {t.foresight.fromSignal}:{' '}
          <Link to={`/senales/${p.signalCode}`} className="font-mono text-accent hover:underline">
            {p.signalCode}
          </Link>{' '}
          · {p.signalTitle}
          {p.hypothesisCode && (
            <>
              {' '}
              · <span className="font-mono">{p.hypothesisCode}</span> {p.hypothesisStatement}
            </>
          )}
        </p>
        {(p.supersedesCode || p.supersededBy.length > 0) && (
          <p className="text-xs text-muted">
            {p.supersedesCode && (
              <>
                {t.foresight.versionOf('')}
                <Link to={`/predicciones/${p.supersedesCode}`} className="font-mono text-accent hover:underline">
                  {p.supersedesCode}
                </Link>
                .{' '}
              </>
            )}
            {p.supersededBy.length > 0 && (
              <>
                {t.foresight.replacedBy}{' '}
                {p.supersededBy.map((c) => (
                  <Link key={c} to={`/predicciones/${c}`} className="font-mono text-accent hover:underline">
                    {c}
                  </Link>
                ))}
                . {p.status === 'Open' && t.foresight.stillCounts}
              </>
            )}
          </p>
        )}
        {canEdit && (
          <div className="flex flex-wrap gap-2 pt-1">
            {p.status === 'Open' && !locked && <Button onClick={() => setMode('correct')}>{t.foresight.actions.correct}</Button>}
            {p.status === 'Open' && p.supersededBy.length === 0 && (
              <Button onClick={() => setMode('supersede')}>{t.foresight.actions.supersede}</Button>
            )}
            {p.status !== 'Withdrawn' && (
              <Button variant={p.isOverdue ? 'primary' : 'secondary'} onClick={() => setMode('resolve')}>
                {p.status === 'Resolved' ? t.foresight.actions.dispute : t.foresight.actions.resolve}
              </Button>
            )}
            {p.status === 'Open' && <Button onClick={() => setMode('withdraw')}>{t.foresight.actions.withdraw}</Button>}
          </div>
        )}
      </header>

      {mode === 'correct' && (
        <PredictionForm mode="correct" initial={p} submit={(i) => foresightApi.correct(p.code, i)} onDone={done} onCancel={() => setMode(null)} />
      )}
      {mode === 'supersede' && (
        <PredictionForm mode="supersede" initial={p} submit={(i) => foresightApi.supersede(p.code, i)} onDone={done} onCancel={() => setMode(null)} />
      )}
      {mode === 'resolve' && <ResolveForm p={p} onDone={done} onCancel={() => setMode(null)} />}
      {mode === 'withdraw' && <WithdrawForm p={p} onDone={done} onCancel={() => setMode(null)} />}

      <Section title={t.foresight.prediction} badge={<LayerBadge layer="observation" />}>
        <p className={`mb-2 flex items-center gap-1 text-[11px] ${locked ? 'text-gold' : 'text-accent'}`}>
          {locked ? <Lock size={11} aria-hidden /> : <Unlock size={11} aria-hidden />}
          {!locked
            ? t.foresight.graceUntil(timeFmt.format(new Date(p.lockedAt)), minutesLeft)
            : p.status === 'Open'
              ? t.foresight.locked(fmtDateTime(p.lockedAt))
              : t.foresight.closed}
        </p>
        <p className="text-base font-medium">{p.statement}</p>
        <div className="mt-3 rounded-md border border-gold/30 bg-gold-soft/30 p-3">
          <p className="text-[11px] font-semibold tracking-wide text-gold uppercase">{t.foresight.criteria}</p>
          <p className="mt-1 whitespace-pre-wrap">{p.resolutionCriteria}</p>
        </div>
        <div className="mt-3 grid gap-3 sm:grid-cols-2">
          <ConfidenceBars confidence={p.confidence} baseRate={p.baseRate} />
          <p className="text-xs text-muted">
            {t.foresight.specificity}: {t.foresight.specificityLevels[p.specificity]}
          </p>
        </div>
        {p.evidenceSnapshot && <p className="mt-2 text-xs whitespace-pre-wrap text-muted">{p.evidenceSnapshot}</p>}
        <p className="mt-2 text-xs text-muted">{t.signals.recordedBy(fmtDateTime(p.recordedAt), p.recordedBy)}</p>
        {p.withdrawnAt && (
          <p className="mt-2 rounded-md border border-line px-3 py-2 text-xs">{t.foresight.withdrawnOn(fmtDateTime(p.withdrawnAt), p.withdrawalReason ?? '')}</p>
        )}
      </Section>

      <Section title={t.foresight.resolutionTitle} badge={<LayerBadge layer="measured" />}>
        {p.resolutions.length === 0 ? (
          <p className="text-muted">{t.foresight.noResolution}</p>
        ) : (
          <ol className="space-y-2">
            {[...p.resolutions].reverse().map((r) => (
              <li key={r.id} className={`rounded-md border p-2.5 ${r.isCurrent ? 'border-accent/50' : 'border-line opacity-80'}`}>
                <div className="flex flex-wrap items-center gap-2 text-xs">
                  <PredictionStatusBadge status="Resolved" overdue={false} outcome={r.outcome} />
                  {r.partialCredit !== null && <span className="font-mono">{r.partialCredit}</span>}
                  <span className={r.isCurrent ? 'text-accent' : 'text-muted'}>{r.isCurrent ? t.foresight.current : t.foresight.replaced}</span>
                  <span className="ml-auto text-muted">
                    {fmtDateTime(r.resolvedAt)} · {r.resolvedBy}
                  </span>
                </div>
                <p className="mt-1 text-sm whitespace-pre-wrap">{r.rationale}</p>
                {r.evidenceUrl && (
                  <a href={r.evidenceUrl} target="_blank" rel="noreferrer noopener" className="block truncate text-xs text-accent underline">
                    {r.evidenceUrl}
                  </a>
                )}
              </li>
            ))}
          </ol>
        )}
      </Section>

      <Section title={`${t.foresight.history} (${p.snapshots.length})`} badge={<LayerBadge layer="observation" />} closed>
        <ol className="space-y-1 font-mono text-[11px] text-muted">
          {p.snapshots.map((s, i) => {
            const snap = JSON.parse(s.snapshot) as Record<string, unknown>
            return (
              <li key={s.chainHash} className="flex flex-wrap gap-x-3">
                <span>#{i + 1}</span>
                <span>{fmtDateTime(s.createdAt)}</span>
                <span>
                  {String(snap.status)} · {String(snap.confidence)}% / {String(snap.base_rate)}% · {String(snap.horizon_date)}
                </span>
                <span title={s.chainHash}>{s.chainHash.slice(0, 12)}…</span>
              </li>
            )
          })}
        </ol>
      </Section>
    </article>
  )
}

function Section({ title, badge, children, closed }: { title: string; badge?: ReactNode; children: ReactNode; closed?: boolean }) {
  return (
    <details open={!closed} className="group rounded-lg border border-line bg-surface">
      <summary className="flex cursor-pointer list-none items-center gap-2 px-3 py-2 text-xs font-semibold tracking-wide text-muted uppercase">
        <ChevronDown size={14} className="-rotate-90 transition-transform group-open:rotate-0" aria-hidden />
        <span className="flex-1">{title}</span>
        {badge}
      </summary>
      <div className="border-t border-line p-3">{children}</div>
    </details>
  )
}

type FormProps = { p: Detail; onDone: (d: Detail) => void; onCancel: () => void }

function useSubmit(onDone: (d: Detail) => void) {
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const run = async (e: FormEvent, action: () => Promise<Detail>) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const d = await action()
      notifyPredictionsChanged()
      onDone(d)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t.errors.unexpected)
    } finally {
      setBusy(false)
    }
  }
  return { error, busy, run }
}

const box = 'space-y-3 rounded-lg border border-accent/40 bg-surface-2/50 p-3'

function ResolveForm({ p, onDone, onCancel }: FormProps) {
  const [outcome, setOutcome] = useState<PredictionOutcome>('Confirmed')
  const [credit, setCredit] = useState('0.5')
  const [rationale, setRationale] = useState('')
  const [url, setUrl] = useState('')
  const { error, busy, run } = useSubmit(onDone)
  return (
    <form
      className={box}
      onSubmit={(e) =>
        run(e, () =>
          foresightApi.resolve(p.code, {
            outcome,
            partialCredit: outcome === 'Partial' ? Number(credit) : null,
            rationale,
            evidenceUrl: url || null,
          }),
        )
      }
    >
      <div className="rounded-md border border-gold/30 bg-gold-soft/30 p-3 text-sm">
        <p className="text-xs text-gold">{t.foresight.resolveHint}</p>
        <p className="mt-1 whitespace-pre-wrap">{p.resolutionCriteria}</p>
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Field label={t.foresight.outcomeLabel}>
          <Select
            value={outcome}
            onChange={(e) => setOutcome(e.target.value as PredictionOutcome)}
            options={Object.entries(t.foresight.outcome).map(([value, label]) => ({ value, label }))}
          />
        </Field>
        {outcome === 'Partial' && (
          <Field label={t.foresight.partialCredit}>
            <TextInput type="number" step="0.05" min={0.05} max={0.95} value={credit} onChange={(e) => setCredit(e.target.value)} />
          </Field>
        )}
      </div>
      <Field label={`${t.foresight.rationaleLabel} *`}>
        <TextArea required minLength={10} rows={3} value={rationale} onChange={(e) => setRationale(e.target.value)} />
      </Field>
      <Field label={t.foresight.evidenceUrl}>
        <TextInput type="url" placeholder="https://" value={url} onChange={(e) => setUrl(e.target.value)} />
      </Field>
      <FormError message={error} />
      <div className="flex justify-end gap-2">
        <Button variant="ghost" onClick={onCancel}>
          {t.signals.cancel}
        </Button>
        <Button variant="primary" type="submit" disabled={busy}>
          {t.foresight.saveResolution}
        </Button>
      </div>
    </form>
  )
}

function WithdrawForm({ p, onDone, onCancel }: FormProps) {
  const [reason, setReason] = useState('')
  const { error, busy, run } = useSubmit(onDone)
  return (
    <form className={box} onSubmit={(e) => run(e, () => foresightApi.withdraw(p.code, reason))}>
      <p className="text-xs text-muted">{t.foresight.withdrawHint}</p>
      <Field label={`${t.foresight.withdrawReason} *`}>
        <TextInput required minLength={10} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Field>
      <FormError message={error} />
      <div className="flex justify-end gap-2">
        <Button variant="ghost" onClick={onCancel}>
          {t.signals.cancel}
        </Button>
        <Button variant="primary" type="submit" disabled={busy}>
          {t.foresight.confirmWithdraw}
        </Button>
      </div>
    </form>
  )
}
