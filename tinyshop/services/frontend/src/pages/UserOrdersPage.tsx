import { Link, useParams } from 'react-router'
import { getUserOrders } from '../api/tinyshop'
import type { Order } from '../api/types'
import { ErrorPanel } from '../components/ErrorPanel'
import { OrderStatusBadge } from '../components/OrderStatusBadge'
import { formatCents, formatTimestamp } from '../lib/format'
import { useApi } from '../lib/useApi'

/** "/users/:id/orders": a user's orders, newest first (the server's order). */
export function UserOrdersPage() {
  const { id = '' } = useParams()
  const orders = useApi((signal) => getUserOrders(id, signal), [id])

  return (
    <section>
      <h2>Orders of user {id}</h2>
      {orders.status === 'loading' && <p className="muted">Loading orders…</p>}
      {orders.status === 'error' && <ErrorPanel error={orders.error} onRetry={orders.reload} />}
      {orders.status === 'success' && <OrderTable orders={orders.data} userId={id} />}
    </section>
  )
}

function OrderTable({ orders, userId }: { orders: Order[]; userId: string }) {
  if (orders.length === 0) {
    // Also what an unknown user gets: the list is filtered, not looked up (contracts.md §5.3).
    return <p className="muted">No orders for user {userId}.</p>
  }

  return (
    <table>
      <thead>
        <tr>
          <th scope="col">Order</th>
          <th scope="col">Product</th>
          <th scope="col" className="number">
            Quantity
          </th>
          <th scope="col" className="number">
            Total
          </th>
          <th scope="col">Status</th>
          <th scope="col">Created</th>
        </tr>
      </thead>
      <tbody>
        {orders.map((order) => (
          <tr key={order.id}>
            <td>
              <Link to={`/orders/${order.id}`}>{order.id}</Link>
            </td>
            <td>{order.productName}</td>
            <td className="number">{order.quantity}</td>
            <td className="number">{formatCents(order.totalCents)}</td>
            <td>
              <OrderStatusBadge status={order.status} />
            </td>
            <td>{formatTimestamp(order.createdAt)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
