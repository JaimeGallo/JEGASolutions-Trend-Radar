import { useState, type FormEvent } from 'react'
import { Lock } from 'lucide-react'
import { Button, Field, FormError, Select, TextArea, TextInput } from '../../components/form'
import { t } from '../../i18n/es'
import { ApiError } from '../../lib/api'
import { notifyPredictionsChanged, type Hypothesis, type PredictionDetail, type PredictionInput } from '../../lib/foresight'

type Mode = 'create' | 'correct' | 'supersede'

/** Mañana en formato AAAA-MM-DD (la fecha límite debe ser posterior a hoy). */
function tomorrow() {
  const d = new Date()
  d.setDate(d.getDate() + 1)
  return d.toISOString().slice(0, 10)
}

/**
 * Formulario de predicción. Cada campo explica para qué sirve: la tasa base y la especificidad
 * son las que permiten medir anticipación (METRICS §3), así que son obligatorias.
 */
export function PredictionForm({
  mode,
  initial,
  hypotheses = [],
  submit,
  onDone,
  onCancel,
}: {
  mode: Mode
  initial?: PredictionDetail
  hypotheses?: Hypothesis[]
  submit: (input: PredictionInput) => Promise<PredictionDetail>
  onDone: (p: PredictionDetail) => void
  onCancel: () => void
}) {
  const [statement, setStatement] = useState(initial?.statement ?? '')
  const [criteria, setCriteria] = useState(initial?.resolutionCriteria ?? '')
  const [horizon, setHorizon] = useState(initial?.horizonDate ?? '')
  const [confidence, setConfidence] = useState(initial?.confidence.toString() ?? '')
  const [baseRate, setBaseRate] = useState(initial?.baseRate.toString() ?? '')
  const [specificity, setSpecificity] = useState(initial?.specificity.toString() ?? '3')
  const [evidence, setEvidence] = useState(initial?.evidenceSnapshot ?? '')
  const [hypothesis, setHypothesis] = useState(initial?.hypothesisCode ?? '')
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const saved = await submit({
        statement,
        resolutionCriteria: criteria,
        horizonDate: horizon,
        confidence: Number(confidence),
        baseRate: Number(baseRate),
        specificity: Number(specificity),
        evidenceSnapshot: evidence || null,
        hypothesisCode: mode === 'create' ? hypothesis || null : null,
        reason: mode === 'create' ? null : reason,
      })
      notifyPredictionsChanged()
      onDone(saved)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t.errors.unexpected)
    } finally {
      setBusy(false)
    }
  }

  const percent = { type: 'number', min: 1, max: 99, required: true } as const

  return (
    <form onSubmit={onSubmit} className="space-y-3 rounded-lg border border-accent/40 bg-surface-2/50 p-3">
      {mode !== 'correct' && (
        <p className="flex gap-2 rounded-md border border-gold/30 bg-gold-soft/30 px-3 py-2 text-xs text-gold">
          <Lock size={13} className="mt-0.5 shrink-0" aria-hidden />
          {t.foresight.lockWarning}
        </p>
      )}
      <Field label={`${t.foresight.statement} *`}>
        <TextArea required rows={2} maxLength={2000} placeholder={t.foresight.statementPlaceholder} value={statement} onChange={(e) => setStatement(e.target.value)} />
      </Field>
      <Field label={`${t.foresight.criteria} *`}>
        <TextArea required rows={2} maxLength={2000} placeholder={t.foresight.criteriaPlaceholder} value={criteria} onChange={(e) => setCriteria(e.target.value)} />
      </Field>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <Field label={`${t.foresight.horizon} *`}>
          <TextInput required type="date" min={tomorrow()} value={horizon} onChange={(e) => setHorizon(e.target.value)} />
        </Field>
        <Field label={`${t.foresight.confidence} *`} hint={t.foresight.confidenceHint}>
          <TextInput {...percent} value={confidence} onChange={(e) => setConfidence(e.target.value)} />
        </Field>
        <Field label={`${t.foresight.baseRate} *`} hint={t.foresight.baseRateHint}>
          <TextInput {...percent} value={baseRate} onChange={(e) => setBaseRate(e.target.value)} />
        </Field>
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Field label={`${t.foresight.specificity} *`}>
          <Select
            value={specificity}
            onChange={(e) => setSpecificity(e.target.value)}
            options={Object.entries(t.foresight.specificityLevels).map(([value, label]) => ({ value, label }))}
          />
        </Field>
        {mode === 'create' && hypotheses.length > 0 && (
          <Field label={t.foresight.hypothesis}>
            <Select
              value={hypothesis}
              onChange={(e) => setHypothesis(e.target.value)}
              options={[{ value: '', label: t.foresight.noHypothesis }, ...hypotheses.map((h) => ({ value: h.code, label: `${h.code} · ${h.statement}` }))]}
            />
          </Field>
        )}
      </div>
      <Field label={t.foresight.evidence}>
        <TextArea rows={2} value={evidence} onChange={(e) => setEvidence(e.target.value)} />
      </Field>
      {mode !== 'create' && (
        <Field label={`${mode === 'correct' ? t.foresight.correctionReason : t.foresight.versionReason} *`}>
          <TextInput required minLength={5} value={reason} onChange={(e) => setReason(e.target.value)} />
        </Field>
      )}
      <FormError message={error} />
      <div className="flex justify-end gap-2">
        <Button variant="ghost" onClick={onCancel}>
          {t.signals.cancel}
        </Button>
        <Button variant="primary" type="submit" disabled={busy}>
          {mode === 'create' ? t.foresight.save : mode === 'correct' ? t.foresight.saveCorrection : t.foresight.saveVersion}
        </Button>
      </div>
    </form>
  )
}
