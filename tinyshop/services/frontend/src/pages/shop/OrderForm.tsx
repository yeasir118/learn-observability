import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { toApiError, type ApiError } from '../../api/client'
import { createOrder } from '../../api/tinyshop'
import type { Product } from '../../api/types'
import { ErrorPanel } from '../../components/ErrorPanel'
import { formatCents } from '../../lib/format'

/**
 * POST /api/orders, then opens the new order. A decline is a normal 201 with status FAILED, so it
 * opens the order too; only errors stay here.
 *
 * No client-side rules beyond "fill in every field": the service that owns the data validates
 * (contracts.md §2.4), and its 400 validation_failed is shown field by field. That keeps one
 * source of truth for the rules, and lets you see the server's answer to a bad request.
 */
export function OrderForm({ products }: { products: Product[] }) {
  const navigate = useNavigate()
  const [userId, setUserId] = useState('')
  const [productId, setProductId] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)

  // Until the user picks one, the first product is selected.
  const selectedProductId = productId || String(products[0]?.id ?? '')

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      const order = await createOrder({
        userId: Number(userId),
        productId: Number(selectedProductId),
        quantity: Number(quantity),
      })
      navigate(`/orders/${order.id}`)
    } catch (caught) {
      // No automatic retry and no "Try again" button: if the first request did reach the server,
      // repeating it would create a second order (a known gap, contracts.md §8).
      setError(toApiError(caught))
      setSubmitting(false)
    }
  }

  return (
    <form className="stacked-form" aria-label="Place an order" onSubmit={handleSubmit}>
      <label>
        User ID
        <input
          name="userId"
          inputMode="numeric"
          placeholder="e.g. 123"
          required
          value={userId}
          onChange={(event) => setUserId(event.target.value)}
        />
      </label>

      <label>
        Product
        <select
          name="productId"
          required
          value={selectedProductId}
          onChange={(event) => setProductId(event.target.value)}
        >
          {products.map((product) => (
            <option key={product.id} value={product.id}>
              {product.name} ({formatCents(product.priceCents)})
            </option>
          ))}
        </select>
      </label>

      <label>
        Quantity
        <input
          name="quantity"
          inputMode="numeric"
          required
          value={quantity}
          onChange={(event) => setQuantity(event.target.value)}
        />
      </label>

      <button type="submit" disabled={submitting || products.length === 0}>
        {submitting ? 'Placing order…' : 'Place order'}
      </button>

      {error && <ErrorPanel error={error} />}
    </form>
  )
}
