import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { Button, Field, FormError, TextArea } from '../../components/form'
import { t } from '../../i18n/es'
import { ApiError } from '../../lib/api'
import { daysToHorizon, foresightApi, PREDICTIONS_CHANGED, type SignalForesight as Foresight } from '../../lib/foresight'
import { PredictionStatusBadge } from './badges'
import { PredictionForm } from './PredictionForm'

/** Hipótesis y predicciones de una señal, dentro de su detalle (sin cambiar de página). */
export function SignalForesight({ signalCode, canEdit }: { signalCode: string; canEdit: boolean }) {
  const [data, setData] = useState<Foresight | null>(null)
  const [mode, setMode] = useState<'hypothesis' | 'prediction' | null>(null)
  const [version, setVersion] = useState(0)
  const navigate = useNavigate()

  useEffect(() => {
    const refresh = () => setVersion((v) => v + 1)
    window.addEventListener(PREDICTIONS_CHANGED, refresh)
    return () => window.removeEventListener(PREDICTIONS_CHANGED, refresh)
  }, [])

  useEffect(() => {
    let alive = true
    foresightApi
      .forSignal(signalCode)
      .then((d) => alive && setData(d))
      .catch(() => undefined)
    return () => {
      alive = false
    }
  }, [signalCode, version])

  if (!data) return null

  return (
    <div className="space-y-3">
      {canEdit && (
        <div className="flex flex-wrap gap-2">
          <Button onClick={() => setMode('prediction')} variant="primary">
            {t.foresight.newPrediction}
          </Button>
          <Button onClick={() => setMode('hypothesis')}>{t.foresight.newHypothesis}</Button>
        </div>
      )}
      {mode === 'hypothesis' && (
        <HypothesisForm
          signalCode={signalCode}
          onDone={(d) => {
            setData(d)
            setMode(null)
          }}
          onCancel={() => setMode(null)}
        />
      )}
      {mode === 'prediction' && (
        <PredictionForm
          mode="create"
          hypotheses={data.hypotheses.filter((h) => h.status === 'Open')}
          submit={(i) => foresightApi.create(signalCode, i)}
          onDone={(p) => navigate(`/predicciones/${p.code}`)}
          onCancel={() => setMode(null)}
        />
      )}

      <div>
        <h3 className="mb-1 text-xs font-semibold text-muted">{t.foresight.hypotheses}</h3>
        {data.hypotheses.length === 0 ? (
          <p className="text-sm text-muted">{t.foresight.noHypotheses}</p>
        ) : (
          <ul className="space-y-1.5">
            {data.hypotheses.map((h) => (
              <li key={h.code} className="rounded-md border border-line p-2 text-sm">
                <span className="font-mono text-[11px] text-gold">{h.code}</span>{' '}
                <span className="text-[11px] text-muted">{t.foresight.hypothesisStatus[h.status]}</span>
                <p>{h.statement}</p>
                {h.rationale && <p className="text-xs text-muted">{h.rationale}</p>}
              </li>
            ))}
          </ul>
        )}
      </div>

      <div>
        <h3 className="mb-1 text-xs font-semibold text-muted">{t.nav.predictions}</h3>
        {data.predictions.length === 0 ? (
          <p className="text-sm text-muted">{t.foresight.noPredictions}</p>
        ) : (
          <ul className="space-y-1.5">
            {data.predictions.map((p) => (
              <li key={p.code}>
                <Link to={`/predicciones/${p.code}`} className="block rounded-md border border-line p-2 hover:border-accent">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-mono text-[11px] text-gold">{p.code}</span>
                    {p.version > 1 && <span className="font-mono text-[10px] text-muted">v{p.version}</span>}
                    <PredictionStatusBadge status={p.status} overdue={p.isOverdue} outcome={p.outcome} />
                    <span className="ml-auto text-[11px] text-muted">
                      {p.status === 'Open' ? t.foresight.days(daysToHorizon(p.horizonDate)) : p.horizonDate} · {p.confidence}% / {p.baseRate}%
                    </span>
                  </div>
                  <p className="mt-0.5 text-sm">{p.statement}</p>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}

function HypothesisForm({ signalCode, onDone, onCancel }: { signalCode: string; onDone: (d: Foresight) => void; onCancel: () => void }) {
  const [statement, setStatement] = useState('')
  const [rationale, setRationale] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      onDone(await foresightApi.createHypothesis(signalCode, statement, rationale || null))
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t.errors.unexpected)
    } finally {
      setBusy(false)
    }
  }
  return (
    <form onSubmit={submit} className="space-y-3 rounded-lg border border-accent/40 bg-surface-2/50 p-3">
      <Field label={`${t.foresight.hypothesisStatement} *`}>
        <TextArea required minLength={10} rows={2} placeholder={t.foresight.hypothesisPlaceholder} value={statement} onChange={(e) => setStatement(e.target.value)} />
      </Field>
      <Field label={t.foresight.rationale}>
        <TextArea rows={2} value={rationale} onChange={(e) => setRationale(e.target.value)} />
      </Field>
      <FormError message={error} />
      <div className="flex justify-end gap-2">
        <Button variant="ghost" onClick={onCancel}>
          {t.signals.cancel}
        </Button>
        <Button variant="primary" type="submit" disabled={busy}>
          {t.foresight.saveHypothesis}
        </Button>
      </div>
    </form>
  )
}
