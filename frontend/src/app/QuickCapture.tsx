import { useEffect, useRef, useState, type FormEvent } from 'react'
import { ChevronDown, Lock } from 'lucide-react'
import { useNavigate } from 'react-router'
import { Button, Field, FormError, Select, TextArea, TextInput } from '../components/form'
import { toOptions } from '../lib/options'
import { t } from '../i18n/es'
import { ApiError } from '../lib/api'
import { notifySignalsChanged, signalsApi, type SignalStage, type SourceType } from '../lib/signals'
import { useDomains } from '../lib/useDomains'

const LAST_DOMAIN = 'tr-last-domain'

function readLastDomain() {
  try {
    return localStorage.getItem(LAST_DOMAIN) ?? ''
  } catch {
    return ''
  }
}

/**
 * Captura rápida (RF-S01, RNF-04): título, texto y dominio bastan. Ctrl+Enter registra.
 * Pensada para menos de 30 segundos: el resto se completa después con versiones y evidencia.
 */
export function QuickCapture({ open, onClose }: { open: boolean; onClose: () => void }) {
  if (!open) return null
  return <QuickCaptureForm onClose={onClose} />
}

function QuickCaptureForm({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate()
  const { topLevel, subdomainsOf } = useDomains()
  const [title, setTitle] = useState('')
  const [text, setText] = useState('')
  const [domain, setDomain] = useState(readLastDomain)
  const [confidence, setConfidence] = useState('')
  const [older, setOlder] = useState(false)
  const [claimedOrigin, setClaimedOrigin] = useState('')
  const [more, setMore] = useState(false)
  const [context, setContext] = useState('')
  const [subdomain, setSubdomain] = useState('')
  const [stage, setStage] = useState<SignalStage>('Intuition')
  const [sourceType, setSourceType] = useState<SourceType>('Observation')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const titleRef = useRef<HTMLInputElement>(null)

  useEffect(() => titleRef.current?.focus(), [])

  const submit = async (e?: FormEvent) => {
    e?.preventDefault()
    if (busy) return
    setBusy(true)
    setError(null)
    try {
      const created = await signalsApi.capture({
        title,
        text,
        domainCode: domain,
        context: context || null,
        subdomainCode: subdomain || null,
        stage,
        sourceType,
        confidence: confidence === '' ? null : Number(confidence),
        claimedOrigin: older ? claimedOrigin || null : null,
      })
      try {
        localStorage.setItem(LAST_DOMAIN, domain)
      } catch {
        // Sin almacenamiento: solo se pierde el dominio recordado.
      }
      notifySignalsChanged()
      onClose()
      navigate(`/senales/${created.code}`)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t.errors.unexpected)
      setBusy(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-auto bg-black/50 px-4 pt-[8vh] pb-8" onMouseDown={onClose}>
      <form
        onSubmit={submit}
        onMouseDown={(e) => e.stopPropagation()}
        onKeyDown={(e) => {
          if (e.key === 'Escape') onClose()
          if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) void submit()
        }}
        aria-label={t.signals.captureTitle}
        className="w-full max-w-xl space-y-3 rounded-xl border border-line bg-surface p-4 shadow-2xl"
      >
        <header className="flex items-center justify-between">
          <h2 className="font-semibold">{t.signals.captureTitle}</h2>
          <span className="flex items-center gap-1 text-[11px] text-gold">
            <Lock size={11} aria-hidden /> {t.signals.immutable}
          </span>
        </header>
        <p className="text-xs text-muted">{t.signals.captureHint}</p>

        <Field label={`${t.signals.title} *`}>
          <TextInput ref={titleRef} required maxLength={200} value={title} onChange={(e) => setTitle(e.target.value)} />
        </Field>
        <Field label={`${t.signals.text} *`}>
          <TextArea required rows={5} placeholder={t.signals.textPlaceholder} value={text} onChange={(e) => setText(e.target.value)} />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label={`${t.signals.domain} *`}>
            <Select
              required
              value={domain}
              onChange={(e) => {
                setDomain(e.target.value)
                setSubdomain('')
              }}
              options={[{ value: '', label: '…' }, ...topLevel.map((d) => ({ value: d.code, label: d.name }))]}
            />
          </Field>
          <Field label={t.signals.confidence}>
            <TextInput type="number" min={0} max={100} value={confidence} onChange={(e) => setConfidence(e.target.value)} />
          </Field>
        </div>

        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={older} onChange={(e) => setOlder(e.target.checked)} />
          {t.signals.older}
        </label>
        {older && (
          <Field label={t.signals.claimedOrigin} hint={t.signals.claimedOriginHint}>
            <TextInput value={claimedOrigin} onChange={(e) => setClaimedOrigin(e.target.value)} placeholder="2025-03" />
          </Field>
        )}

        <button type="button" onClick={() => setMore((m) => !m)} className="flex items-center gap-1 text-xs text-muted hover:text-ink">
          <ChevronDown size={14} className={more ? 'rotate-180' : ''} aria-hidden />
          {t.signals.moreOptions}
        </button>
        {more && (
          <div className="space-y-3">
            <Field label={t.signals.context}>
              <TextArea rows={2} placeholder={t.signals.contextPlaceholder} value={context} onChange={(e) => setContext(e.target.value)} />
            </Field>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
              <Field label={t.signals.subdomain}>
                <Select
                  value={subdomain}
                  onChange={(e) => setSubdomain(e.target.value)}
                  options={[{ value: '', label: t.signals.noSubdomain }, ...subdomainsOf(domain).map((d) => ({ value: d.code, label: d.name }))]}
                />
              </Field>
              <Field label={t.signals.stage}>
                <Select value={stage} onChange={(e) => setStage(e.target.value as SignalStage)} options={toOptions(t.enums.stage)} />
              </Field>
              <Field label={t.signals.sourceType}>
                <Select value={sourceType} onChange={(e) => setSourceType(e.target.value as SourceType)} options={toOptions(t.enums.sourceType)} />
              </Field>
            </div>
          </div>
        )}

        <FormError message={error} />
        <footer className="flex items-center justify-end gap-2">
          <span className="mr-auto text-[11px] text-muted">
            <kbd>Ctrl</kbd> + <kbd>Enter</kbd>
          </span>
          <Button variant="ghost" onClick={onClose}>
            {t.signals.cancel}
          </Button>
          <Button variant="primary" type="submit" disabled={busy}>
            {busy ? t.signals.saving : t.signals.save}
          </Button>
        </footer>
      </form>
    </div>
  )
}
