import { useCallback, useEffect, useState } from 'react'
import { ShieldAlert, ShieldCheck } from 'lucide-react'
import { LayerBadge, Panel } from '../../components/ui'
import { t } from '../../i18n/es'
import { api, ApiError, type AuditEntry, type IntegrityReport } from '../../lib/api'

async function fetchIntegrity() {
  try {
    const [report, entries] = await Promise.all([
      api<IntegrityReport>('/api/integrity/verify'),
      api<AuditEntry[]>('/api/audit?take=200'),
    ])
    return { report, entries }
  } catch (err) {
    return { error: err instanceof ApiError ? err.message : t.errors.network }
  }
}

const dateFmt = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'medium' })

export function IntegrityPage() {
  const [report, setReport] = useState<IntegrityReport | null>(null)
  const [entries, setEntries] = useState<AuditEntry[]>([])
  const [busy, setBusy] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const apply = useCallback((result: { report: IntegrityReport; entries: AuditEntry[] } | { error: string }) => {
    if ('error' in result) {
      setError(result.error)
    } else {
      setReport(result.report)
      setEntries(result.entries)
      setError(null)
    }
    setBusy(false)
  }, [])

  useEffect(() => {
    fetchIntegrity().then(apply)
  }, [apply])

  const verifyAgain = () => {
    setBusy(true)
    fetchIntegrity().then(apply)
  }

  return (
    <div className="flex h-full min-h-0 flex-col gap-3 p-3">
      <header className="flex shrink-0 flex-wrap items-center gap-3">
        <h1 className="text-base font-semibold">{t.integrity.title}</h1>
        <LayerBadge layer="measured" />
        <button
          type="button"
          onClick={verifyAgain}
          disabled={busy}
          className="ml-auto rounded-md border border-line px-3 py-1 hover:border-accent disabled:opacity-60"
        >
          {busy ? t.integrity.verifying : t.integrity.verify}
        </button>
      </header>

      <div className="grid shrink-0 grid-cols-1 gap-3 md:grid-cols-[1fr_2fr]">
        <div className="rounded-lg border border-line bg-surface p-3">
          {error && <p className="text-danger">{error}</p>}
          {report && (
            <div className="flex items-start gap-3">
              {report.isValid ? (
                <ShieldCheck className="text-ok" size={28} aria-hidden />
              ) : (
                <ShieldAlert className="text-danger" size={28} aria-hidden />
              )}
              <div>
                <p className={`font-semibold ${report.isValid ? 'text-ok' : 'text-danger'}`}>
                  {report.isValid ? t.integrity.valid : t.integrity.broken}
                </p>
                <ul className="text-muted">
                  {report.chains.map((c) => (
                    <li key={c.chain}>
                      <span className="font-mono text-xs">{t.integrity.chains[c.chain] ?? c.chain}</span>:{' '}
                      {c.isValid ? t.integrity.checked(c.checkedEntries) : t.integrity.invalid(c.firstInvalidEntryId ?? 0)}
                    </li>
                  ))}
                </ul>
                <p className="font-mono text-xs text-muted">
                  {t.integrity.at}: {dateFmt.format(new Date(report.verifiedAt))}
                </p>
              </div>
            </div>
          )}
        </div>
        <p className="rounded-lg border border-line bg-surface p-3 text-muted">{t.integrity.explanation}</p>
      </div>

      <Panel title={t.integrity.audit} className="flex-1">
        <table className="w-full text-left text-xs">
          <thead className="sticky top-0 bg-surface text-muted">
            <tr>
              <th className="px-3 py-2 font-medium">{t.integrity.columns.id}</th>
              <th className="px-3 py-2 font-medium">{t.integrity.columns.when}</th>
              <th className="px-3 py-2 font-medium">{t.integrity.columns.action}</th>
              <th className="px-3 py-2 font-medium">{t.integrity.columns.entity}</th>
              <th className="px-3 py-2 font-medium">{t.integrity.columns.hash}</th>
            </tr>
          </thead>
          <tbody>
            {entries.map((e) => (
              <tr key={e.id} className="border-t border-line">
                <td className="px-3 py-1.5 font-mono text-muted">{e.id}</td>
                <td className="px-3 py-1.5 font-mono whitespace-nowrap">{dateFmt.format(new Date(e.occurredAt))}</td>
                <td className="px-3 py-1.5">{e.action}</td>
                <td className="px-3 py-1.5 text-muted">{e.entityType}</td>
                <td className="px-3 py-1.5 font-mono text-muted" title={e.chainHash}>
                  {e.chainHash.slice(0, 16)}…
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Panel>
    </div>
  )
}
