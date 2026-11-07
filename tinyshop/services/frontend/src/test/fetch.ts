// Test helpers: real Response objects, so the client is tested against actual headers and bodies.
import { vi } from 'vitest'

export function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json; charset=utf-8' },
  })
}

/** A Problem Details response (contracts.md §2.1). */
export function problem(status: number, fields: Record<string, unknown>): Response {
  return new Response(JSON.stringify({ status, ...fields }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

/**
 * Replaces fetch with a function that answers by "METHOD path", e.g. "GET /api/products".
 * A request without a route fails the test.
 */
export function mockFetch(routes: Record<string, () => Response | Promise<Response>>) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const key = `${init?.method ?? 'GET'} ${String(input)}`
    const route = routes[key]
    if (!route) {
      throw new Error(`Unexpected request: ${key}`)
    }
    return route()
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}
