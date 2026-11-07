import { useCallback, useEffect, useState, type DependencyList } from 'react'
import { toApiError, type ApiError } from '../api/client'

export type ApiState<T> =
  | { status: 'loading' }
  | { status: 'success'; data: T }
  | { status: 'error'; error: ApiError }

/**
 * Loads data when the page opens and again whenever `deps` change (for example the order ID in
 * the URL). The previous call is aborted, so a slow old answer can never overwrite a newer one.
 * `reload` repeats the call, for a "Try again" button.
 */
export function useApi<T>(
  load: (signal: AbortSignal) => Promise<T>,
  deps: DependencyList,
): ApiState<T> & { reload: () => void } {
  const [state, setState] = useState<ApiState<T>>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setState({ status: 'loading' })
    load(controller.signal).then(
      (data) => {
        if (!controller.signal.aborted) {
          setState({ status: 'success', data })
        }
      },
      (error: unknown) => {
        if (!controller.signal.aborted) {
          setState({ status: 'error', error: toApiError(error) })
        }
      },
    )
    return () => controller.abort()
    // `load` is a new function on every render; `deps` says when the data actually changes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, attempt])

  const reload = useCallback(() => setAttempt((n) => n + 1), [])
  return { ...state, reload }
}
