import { describe, expect, it, vi } from 'vitest'
import { json, mockFetch, problem } from '../test/fetch'
import { ApiError, postJson, request } from './client'

describe('request', () => {
  it('returns the parsed body of a success', async () => {
    mockFetch({ 'GET /api/products': () => json({ items: [{ id: 1, name: 'Laptop', priceCents: 100000 }] }) })

    await expect(request('/api/products')).resolves.toEqual({
      items: [{ id: 1, name: 'Laptop', priceCents: 100000 }],
    })
  })

  it('keeps every Problem Details field of an error', async () => {
    mockFetch({
      'POST /api/orders': () =>
        problem(422, {
          title: 'User not found',
          code: 'user_not_found',
          detail: 'User 999 does not exist.',
          traceId: '00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01',
        }),
    })

    const error = await postJson('/api/orders', { userId: 999 }).catch((caught: unknown) => caught)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({
      status: 422,
      title: 'User not found',
      code: 'user_not_found',
      detail: 'User 999 does not exist.',
      traceId: '00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01',
      orderId: null,
    })
  })

  it('keeps validation errors and the orderId extension', async () => {
    mockFetch({
      'GET /api/a': () =>
        problem(400, { title: 'Validation failed', code: 'validation_failed', errors: { quantity: ['Must be between 1 and 100.'] } }),
      'GET /api/b': () => problem(502, { title: 'Payment unavailable', code: 'payment_unavailable', orderId: 1001 }),
    })

    await expect(request('/api/a')).rejects.toMatchObject({ errors: { quantity: ['Must be between 1 and 100.'] } })
    await expect(request('/api/b')).rejects.toMatchObject({ orderId: 1001 })
  })

  it('reports an error that is not Problem Details by its status', async () => {
    // For example an HTML error page from a proxy in front of the gateway (TS-015).
    mockFetch({
      'GET /api/products': () =>
        new Response('<html><body>502 Bad Gateway</body></html>', {
          status: 502,
          statusText: 'Bad Gateway',
          headers: { 'Content-Type': 'text/html' },
        }),
    })

    await expect(request('/api/products')).rejects.toMatchObject({ status: 502, title: 'Bad Gateway', code: null })
  })

  it('reports an empty error body by its status', async () => {
    // The gateway passes an upstream's empty error through unchanged (TS-014).
    mockFetch({ 'GET /api/users/42': () => new Response(null, { status: 404 }) })

    await expect(request('/api/users/42')).rejects.toMatchObject({ status: 404, title: 'HTTP 404' })
  })

  it('reports no response at all with a null status', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    await expect(request('/api/products')).rejects.toMatchObject({ status: null, title: 'The server could not be reached' })
  })

  it('rethrows an abort instead of reporting it', async () => {
    const controller = new AbortController()
    controller.abort()
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new DOMException('Aborted', 'AbortError')))

    const error = await request('/api/products', { signal: controller.signal }).catch((caught: unknown) => caught)

    expect(error).not.toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ name: 'AbortError' })
  })

  it('sends JSON with its content type', async () => {
    const fetchMock = mockFetch({ 'POST /api/orders': () => json({ id: 1001 }, 201) })

    await postJson('/api/orders', { userId: 123, productId: 2, quantity: 1 })

    const init = fetchMock.mock.calls[0]?.[1]
    expect(new Headers(init?.headers).get('Content-Type')).toBe('application/json')
    expect(init?.body).toBe('{"userId":123,"productId":2,"quantity":1}')
  })
})
