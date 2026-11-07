// The resources of contracts.md §3, as the public API returns them.
// Money is integer cents (formatted for display only); timestamps are UTC RFC 3339 strings.

export interface Product {
  id: number
  name: string
  priceCents: number
}

export interface User {
  id: number
  name: string
}

export type OrderStatus = 'PENDING' | 'PAID' | 'FAILED'

export interface Order {
  id: number
  userId: number
  productId: number
  productName: string
  unitPriceCents: number
  quantity: number
  totalCents: number
  status: OrderStatus
  /** Set only when FAILED: the decline reason, or payment_unavailable. */
  failureReason: string | null
  /** Set only when PAID. */
  transactionId: string | null
  createdAt: string
  updatedAt: string
}

/** The body of POST /api/orders. */
export interface NewOrder {
  userId: number
  productId: number
  quantity: number
}

/** Collections are always wrapped (contracts.md §1). */
export interface Collection<T> {
  items: T[]
}
