import { Link, useParams } from 'react-router'
import { getOrder } from '../api/tinyshop'
import type { Order } from '../api/types'
import { ErrorPanel } from '../components/ErrorPanel'
import { OrderStatusBadge } from '../components/OrderStatusBadge'
import { formatCents, formatTimestamp } from '../lib/format'
import { useApi } from '../lib/useApi'

/** "/orders/:id": one order. */
export function OrderPage() {
  const { id = '' } = useParams()
  const order = useApi((signal) => getOrder(id, signal), [id])

  return (
    <section>
      <h2>Order {id}</h2>
      {order.status === 'loading' && <p className="muted">Loading order…</p>}
      {order.status === 'error' && <ErrorPanel error={order.error} onRetry={order.reload} />}
      {order.status === 'success' && <OrderDetails order={order.data} />}
    </section>
  )
}

function OrderDetails({ order }: { order: Order }) {
  return (
    <>
      <p>
        <OrderStatusBadge status={order.status} /> <PaymentOutcome order={order} />
      </p>

      <dl className="facts">
        <dt>Product</dt>
        <dd>
          {order.productName} (product {order.productId})
        </dd>
        <dt>Unit price</dt>
        <dd>{formatCents(order.unitPriceCents)}</dd>
        <dt>Quantity</dt>
        <dd>{order.quantity}</dd>
        <dt>Total</dt>
        <dd>
          <strong>{formatCents(order.totalCents)}</strong>
        </dd>
        <dt>User</dt>
        <dd>
          <Link to={`/users/${order.userId}/orders`}>user {order.userId}</Link>
        </dd>
        <dt>Created</dt>
        <dd>{formatTimestamp(order.createdAt)}</dd>
        <dt>Updated</dt>
        <dd>{formatTimestamp(order.updatedAt)}</dd>
      </dl>
    </>
  )
}

function PaymentOutcome({ order }: { order: Order }) {
  switch (order.status) {
    case 'PAID':
      return (
        <>
          Transaction <code className="selectable">{order.transactionId}</code>
        </>
      )
    case 'FAILED':
      return (
        <>
          Payment failed: <code>{order.failureReason}</code>
        </>
      )
    case 'PENDING':
      // A payment timeout leaves the outcome unknown (contracts.md §8, known gaps).
      return <>The payment outcome isn't known yet.</>
  }
}
