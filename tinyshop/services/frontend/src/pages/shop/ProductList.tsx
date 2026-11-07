import type { Product } from '../../api/types'
import { formatCents } from '../../lib/format'

export function ProductList({ products }: { products: Product[] }) {
  if (products.length === 0) {
    return <p className="muted">The catalogue is empty.</p>
  }

  return (
    <table>
      <thead>
        <tr>
          <th scope="col">ID</th>
          <th scope="col">Product</th>
          <th scope="col" className="number">
            Price
          </th>
        </tr>
      </thead>
      <tbody>
        {products.map((product) => (
          <tr key={product.id}>
            <td>{product.id}</td>
            <td>{product.name}</td>
            <td className="number">{formatCents(product.priceCents)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
