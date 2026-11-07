// A small fetch wrapper. Every failed call becomes one ApiError, whatever went wrong:
//   - a Problem Details response (contracts.md §2.1): title, code, traceId and the rest are kept
//   - another error response, for example an HTML page from a proxy in front of the gateway
//   - no response at all: the server couldn't be reached
// Pages then need only one way to show an error.

export interface ApiErrorInit {
  /** The HTTP status, or null when no response arrived. */
  status: number | null
  title: string
  code?: string | null
  detail?: string | null
  traceId?: string | null
  /** Order errors raised after the order row exists carry its ID (contracts.md §2.1). */
  orderId?: number | null
  /** Validation errors: field name → messages. */
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number | null
  readonly title: string
  readonly code: string | null
  readonly detail: string | null
  readonly traceId: string | null
  readonly orderId: number | null
  readonly errors: Record<string, string[]>

  constructor(init: ApiErrorInit) {
    super(init.detail ?? init.title)
    this.name = 'ApiError'
    this.status = init.status
    this.title = init.title
    this.code = init.code ?? null
    this.detail = init.detail ?? null
    this.traceId = init.traceId ?? null
    this.orderId = init.orderId ?? null
    this.errors = init.errors ?? {}
  }
}

/**
 * Calls the API and returns the parsed JSON body. Paths are always relative (/api/...): the app
 * is served from the same origin as the API, by Vite in development and by nginx in the container.
 * Throws ApiError on failure; an aborted call rethrows the AbortError untouched.
 */
export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json, application/problem+json')

  let response: Response
  try {
    response = await fetch(path, { ...init, headers })
  } catch (error) {
    // Cancelled by us (the page changed): not a failure to show.
    if (init.signal?.aborted) {
      throw error
    }
    throw new ApiError({
      status: null,
      title: 'The server could not be reached',
      detail: 'No response arrived. Check your connection, or whether TinyShop is running.',
    })
  }

  if (!response.ok) {
    throw await errorFrom(response)
  }

  try {
    return (await response.json()) as T
  } catch {
    throw new ApiError({ status: response.status, title: 'The response could not be read' })
  }
}

/** Sends a JSON body. */
export function postJson<T>(path: string, body: unknown, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Content-Type', 'application/json')
  return request<T>(path, { ...init, method: 'POST', headers, body: JSON.stringify(body) })
}

/** Anything a call can throw, as an ApiError (for code that catches unknown errors). */
export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error
  }
  return new ApiError({
    status: null,
    title: 'Something went wrong',
    detail: error instanceof Error ? error.message : String(error),
  })
}

async function errorFrom(response: Response): Promise<ApiError> {
  const fallbackTitle = response.statusText || `HTTP ${response.status}`
  const contentType = response.headers.get('Content-Type') ?? ''

  if (contentType.includes('json')) {
    try {
      const body: unknown = await response.json()
      if (isRecord(body)) {
        return new ApiError({
          status: response.status,
          title: stringOrNull(body.title) ?? fallbackTitle,
          code: stringOrNull(body.code),
          detail: stringOrNull(body.detail),
          traceId: stringOrNull(body.traceId),
          orderId: typeof body.orderId === 'number' ? body.orderId : null,
          errors: validationErrors(body.errors),
        })
      }
    } catch {
      // Not valid JSON after all: treated like any other non-Problem-Details error below.
    }
  }

  // Not Problem Details: an empty body, or a page from a proxy in front of the gateway.
  return new ApiError({ status: response.status, title: fallbackTitle })
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function stringOrNull(value: unknown): string | null {
  return typeof value === 'string' && value.length > 0 ? value : null
}

function validationErrors(value: unknown): Record<string, string[]> {
  if (!isRecord(value)) {
    return {}
  }
  const result: Record<string, string[]> = {}
  for (const [field, messages] of Object.entries(value)) {
    if (Array.isArray(messages)) {
      result[field] = messages.filter((message): message is string => typeof message === 'string')
    }
  }
  return result
}
