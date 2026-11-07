// The five public routes of contracts.md §4, and nothing else. IDs typed by the user are passed
// on as typed: the service that owns the data validates them (contracts.md §2.4) and answers
// 400 validation_failed or 404, which the page shows like any other error.
import { postJson, request } from './client'
import type { Collection, NewOrder, Order, Product, User } from './types'

/** GET /api/products */
export async function getProducts(signal?: AbortSignal): Promise<Product[]> {
  const products = await request<Collection<Product>>('/api/products', { signal })
  return products.items
}

/** GET /api/users/{id} */
export function getUser(id: string, signal?: AbortSignal): Promise<User> {
  return request<User>(`/api/users/${encodeURIComponent(id)}`, { signal })
}

/** POST /api/orders: 201 with the order, PAID or FAILED (a decline isn't an error). */
export function createOrder(order: NewOrder): Promise<Order> {
  return postJson<Order>('/api/orders', order)
}

/** GET /api/orders/{id} */
export function getOrder(id: string, signal?: AbortSignal): Promise<Order> {
  return request<Order>(`/api/orders/${encodeURIComponent(id)}`, { signal })
}

/** GET /api/orders?userId={id}: newest first; an unknown user simply has none. */
export async function getUserOrders(userId: string, signal?: AbortSignal): Promise<Order[]> {
  const query = new URLSearchParams({ userId })
  const orders = await request<Collection<Order>>(`/api/orders?${query}`, { signal })
  return orders.items
}
