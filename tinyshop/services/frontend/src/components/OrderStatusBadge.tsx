import type { OrderStatus } from '../api/types'

const labels: Record<OrderStatus, string> = {
  PAID: 'Paid',
  FAILED: 'Failed',
  PENDING: 'Pending',
}

export function OrderStatusBadge({ status }: { status: OrderStatus }) {
  return <span className={`badge badge-${status.toLowerCase()}`}>{labels[status]}</span>
}
