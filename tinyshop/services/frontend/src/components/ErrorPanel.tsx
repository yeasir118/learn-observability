import { Link } from 'react-router'
import type { ApiError } from '../api/client'

interface ErrorPanelProps {
  error: ApiError
  /** Shown as a "Try again" button. Only for reads: repeating a POST could create a second order. */
  onRetry?: () => void
}

/**
 * Shows a failed call the way contracts.md §2.1 describes it. The trace ID is what to search the
 * logs for (Failure Exercise 1, M2), so it's shown in full and easy to select.
 */
export function ErrorPanel({ error, onRetry }: ErrorPanelProps) {
  const fieldErrors = Object.entries(error.errors)

  return (
    <div className="error" role="alert">
      <p className="error-title">{error.title}</p>
      {error.detail && <p>{error.detail}</p>}

      {fieldErrors.length > 0 && (
        <ul>
          {fieldErrors.map(([field, messages]) => (
            <li key={field}>
              <code>{field}</code>: {messages.join(' ')}
            </li>
          ))}
        </ul>
      )}

      <dl className="error-facts">
        <dt>Status</dt>
        <dd>{error.status ?? 'no response'}</dd>
        {error.code && (
          <>
            <dt>Code</dt>
            <dd>
              <code>{error.code}</code>
            </dd>
          </>
        )}
        {error.traceId && (
          <>
            <dt>Trace ID</dt>
            <dd>
              <code className="selectable">{error.traceId}</code>
            </dd>
          </>
        )}
      </dl>

      {error.orderId !== null && (
        <p>
          The order was created: <Link to={`/orders/${error.orderId}`}>view order {error.orderId}</Link>.
        </p>
      )}

      {onRetry && (
        <button type="button" onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  )
}
