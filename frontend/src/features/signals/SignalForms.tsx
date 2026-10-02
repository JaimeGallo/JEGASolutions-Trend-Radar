import { useState, type FormEvent } from 'react'
import { Button, Field, FormError, Select, TextArea, TextInput } from '../../components/form'
import { toOptions } from '../../lib/options'
import { t } from '../../i18n/es'
import { ApiError } from '../../lib/api'
import { localToIso } from '../../lib/format'
import {
  notifySignalsChanged,
  signalsApi,
  type Confidentiality,
  type EvidenceKind,
  type EvidenceRole,
  type SignalDetail,
  type SignalStage,
  type SignalStatus,
  type TimestampAuthority,
} from '../../lib/signals'
import { useDomains } from '../../lib/useDomains'

type FormProps = { signal: SignalDetail; onDone: (updated: SignalDetail) => void; onCancel: () => void }

/** Envía, muestra el error en español y notifica a las listas. */
function useSubmit(onDone: (s: SignalDetail) => void) {
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const run = async (e: FormEvent, action: () => Promise<SignalDetail>) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const updated = await action()
      notifySignalsChanged()
      onDone(updated)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t.errors.unexpected)
    } finally {
      setBusy(false)
    }
  }
  return { error, busy, run }
}

const box = 'space-y-3 rounded-lg border border-accent/40 bg-surface-2/50 p-3'

export function ReviseForm({ signal, onDone, onCancel }: FormProps) {
  const current = signal.versions[signal.versions.length - 1]
  const [title, setTitle] = useState(current.title)
  const [text, setText] = useState(current.text)
  const [context, setContext] = useState(current.context ?? '')
  const [confidence, setConfidence] = useState(current.confidence?.toString() ?? '')
  const [reason, setReason] = useState('')
  const { error, busy, run } = useSubmit(onDone)

  return (
    <form
      className={box}
      onSubmit={(e) =>
        run(e, () =>
          signalsApi.revise(signal.code, {
            title,
            text,
            context: context || null,
            confidence: confidence === '' ? null : Number(confidence),
            reason,
          }),
        )
      }
    >
      <p className="text-xs text-muted">{t.signals.reviseHint}</p>
      <Field label={t.signals.title}>
        <TextInput required maxLength={200} value={title} onChange={(e) => setTitle(e.target.value)} />
      </Field>
      <Field label={t.signals.text}>
        <TextArea required rows={5} value={text} onChange={(e) => setText(e.target.value)} />
      </Field>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-[1fr_10rem]">
        <Field label={t.signals.context}>
          <TextInput value={context} onChange={(e) => setContext(e.target.value)} />
        </Field>
        <Field label={t.signals.confidence}>
          <TextInput type="number" min={0} max={100} value={confidence} onChange={(e) => setConfidence(e.target.value)} />
        </Field>
      </div>
      <Field label={`${t.signals.reason} *`}>
        <TextInput required minLength={5} placeholder={t.signals.reasonPlaceholder} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Field>
      <FormError message={error} />
      <div className="flex justify-end gap-2">
        <Button variant="ghost" onClick={onCancel}>
          {t.signals.cancel}
        </Button>
        <Button variant="primary" type="submit" disabled={busy}>
          {t.signals.saveVersion}
        </Button>
      </div>
    </form>
  )
}

const scale = [{ value: '', label: '·' }, ...[1, 2, 3, 4, 5].map((n) => ({ value: String(n), label: String(n) }))]

export function ClassifyForm({ signal, onDone, onCancel }: FormProps) {
  const { subdomainsOf } = useDomains()
  const [stage, setStage] = useState<SignalStage>(signal.stage)
  const [status, setStatus] = useState<SignalStatus>(signal.status)
  const [subdomain, setSubdomain] = useState(signal.subdomainCode ?? '')
  const [geo, setGeo] = useState(signal.geographicScope ?? '')
  const [impact, setImpact] = useState(signal.impactEstimate?.toString() ?? '')
  const [relevance, setRelevance] = useState(signal.relevanceToJegas?.toString() ?? '')
  const [confidentiality, setConfidentiality] = useState<Confidentiality>(signal.confidentiality)
  const [sourceReference, setSourceReference] = useState(signal.sourceReference ?? '')
  const [reason, setReason] = useState('')
  const { error, busy, run } = useSubmit(onDone)

  return (
    <form
      className={box}
      onSubmit={(e) =>
        run(e, () =>
          signalsApi.reclassify(signal.code, {
            stage,
            status,
            subdomainCode: subdomain || null,
            geographicScope: geo || null,
            impactEstimate: impact ? Number(impact) : null,
            relevanceToJegas: relevance ? Number(relevance) : null,
            confidentiality,
            sourceReference: sourceReference || null,
            reason,
          }),
        )
      }
    >
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        <Field label={t.signals.stage}>
          <Select value={stage} onChange={(e) => setStage(e.target.value as SignalStage)} options={toOptions(t.enums.stage)} />
        </Field>
        <Field label={t.signals.status}>
          <Select value={status} onChange={(e) => setStatus(e.target.value as SignalStatus)} options={toOptions(t.enums.status)} />
        </Field>
        <Field label={t.signals.subdomain}>
          <Select
            value={subdomain}
            onChange={(e) => setSubdomain(e.target.value)}
            options={[{ value: '', label: t.signals.noSubdomain }, ...subdomainsOf(signal.domainCode).map((d) => ({ value: d.code, label: d.name }))]}
          />
        </Field>
        <Field label={t.signals.impact}>
          <Select value={impact} onChange={(e) => setImpact(e.target.value)} options={scale} />
        </Field>
        <Field label={t.signals.relevance}>
          <Select value={relevance} onChange={(e) => setRelevance(e.target.value)} options={scale} />
        </Field>
        <Field label={t.signals.confidentiality}>
          <Select
            value={confidentiality}
            onChange={(e) => setConfidentiality(e.target.value as Confidentiality)}
            options={toOptions(t.enums.confidentiality)}
          />
        </Field>
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Field label={t.signals.geographicScope}>
          <TextInput value={geo} onChange={(e) => setGeo(e.target.value)} placeholder="Medellín, Colombia, LatAm" />
        </Field>
        <Field label={t.signals.sourceReference}>
          <TextInput value={sourceReference} onChange={(e) => setSourceReference(e.target.value)} />
        </Field>
      </div>
      <Field label={`${t.signals.reason} *`}>
        <TextInput required minLength={5} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Field>
      <FormError message={error} />
      <div className="flex justify-end gap-2">
        <Button variant="ghost" onClick={onCancel}>
          {t.signals.cancel}
        </Button>
        <Button variant="primary" type="submit" disabled={busy}>
          {t.signals.saveClassification}
        </Button>
      </div>
    </form>
  )
}

export function EvidenceForm({ signal, onDone, onCancel }: FormProps) {
  const [kind, setKind] = useState<EvidenceKind>('Url')
  const [role, setRole] = useState<EvidenceRole>(signal.origin.isRetrospective ? 'Origin' : 'Supports')
  const [description, setDescription] = useState('')
  const [url, setUrl] = useState('')
  const [repo, setRepo] = useState('')
  const [commit, setCommit] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [date, setDate] = useState('')
  const [authority, setAuthority] = useState<TimestampAuthority>('None')
  const { error, busy, run } = useSubmit(onDone)

  const submit = (e: FormEvent) => {
    const artifactTimestamp = localToIso(date)
    const timestampAuthority = artifactTimestamp ? authority : 'None'
    return run(e, () =>
      kind === 'File'
        ? signalsApi.addFile(signal.code, file!, { role, description, artifactTimestamp, timestampAuthority })
        : signalsApi.addEvidence(signal.code, {
            kind,
            role,
            description,
            url: url || null,
            gitRepo: repo || null,
            gitCommit: commit || null,
            artifactTimestamp,
            timestampAuthority,
          }),
    )
  }

  return (
    <form className={box} onSubmit={submit}>
      <p className="text-xs text-muted">{t.signals.evidence.appendOnly}</p>
      <div className="flex flex-wrap gap-1" role="tablist" aria-label={t.signals.evidence.kind}>
        {(['Url', 'GitCommit', 'File', 'Note'] as const).map((k) => (
          <button
            key={k}
            type="button"
            role="tab"
            aria-selected={kind === k}
            onClick={() => setKind(k)}
            className={`rounded-md px-2.5 py-1 text-xs ${kind === k ? 'bg-accent text-white' : 'border border-line text-muted hover:text-ink'}`}
          >
            {t.enums.kind[k]}
          </button>
        ))}
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-[12rem_1fr]">
        <Field label={t.signals.evidence.role}>
          <Select value={role} onChange={(e) => setRole(e.target.value as EvidenceRole)} options={toOptions(t.enums.role)} />
        </Field>
        <Field label={`${t.signals.evidence.description} *`}>
          <TextInput required placeholder={t.signals.evidence.descriptionPlaceholder} value={description} onChange={(e) => setDescription(e.target.value)} />
        </Field>
      </div>
      {kind === 'Url' && (
        <Field label={`${t.signals.evidence.url} *`}>
          <TextInput required type="url" placeholder="https://" value={url} onChange={(e) => setUrl(e.target.value)} />
        </Field>
      )}
      {kind === 'GitCommit' && (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label={`${t.signals.evidence.repo} *`}>
            <TextInput required placeholder="JaimeGallo/jegasolutions-platform" value={repo} onChange={(e) => setRepo(e.target.value)} />
          </Field>
          <Field label={`${t.signals.evidence.commit} *`}>
            <TextInput required pattern="[0-9a-fA-F]{7,40}" className="font-mono" value={commit} onChange={(e) => setCommit(e.target.value)} />
          </Field>
        </div>
      )}
      {kind === 'File' && (
        <Field label={`${t.signals.evidence.file} *`}>
          <input
            required
            type="file"
            accept=".png,.jpg,.jpeg,.webp,.gif,.pdf,.txt,.md,.csv,.json,.ots"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            className="block w-full text-sm text-muted file:mr-3 file:rounded-md file:border file:border-line file:bg-surface file:px-3 file:py-1 file:text-ink"
          />
        </Field>
      )}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Field label={t.signals.evidence.date}>
          <TextInput type="datetime-local" value={date} onChange={(e) => setDate(e.target.value)} />
        </Field>
        <Field label={t.signals.evidence.authority}>
          <Select
            disabled={!date}
            value={date ? authority : 'None'}
            onChange={(e) => setAuthority(e.target.value as TimestampAuthority)}
            options={toOptions(t.enums.authority)}
          />
        </Field>
      </div>
      <FormError message={error} />
      <div className="flex justify-end gap-2">
        <Button variant="ghost" onClick={onCancel}>
          {t.signals.cancel}
        </Button>
        <Button variant="primary" type="submit" disabled={busy}>
          {t.signals.evidence.save}
        </Button>
      </div>
    </form>
  )
}
