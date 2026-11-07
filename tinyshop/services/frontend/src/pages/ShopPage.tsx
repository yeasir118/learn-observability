import { getProducts } from '../api/tinyshop'
import { ErrorPanel } from '../components/ErrorPanel'
import { useApi } from '../lib/useApi'
import { OrderForm } from './shop/OrderForm'
import { ProductList } from './shop/ProductList'
import { UserLookup } from './shop/UserLookup'

/** "/": products, user lookup and the order form. */
export function ShopPage() {
  const products = useApi((signal) => getProducts(signal), [])

  return (
    <>
      <section>
        <h2>Products</h2>
        {products.status === 'loading' && <p className="muted">Loading products…</p>}
        {products.status === 'error' && <ErrorPanel error={products.error} onRetry={products.reload} />}
        {products.status === 'success' && <ProductList products={products.data} />}
      </section>

      <section>
        <h2>Find a user</h2>
        <UserLookup />
      </section>

      <section>
        <h2>Place an order</h2>
        {products.status === 'success' ? (
          <OrderForm products={products.data} />
        ) : (
          <p className="muted">The order form needs the product list.</p>
        )}
      </section>
    </>
  )
}
