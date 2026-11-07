import { Link, Route, Routes } from 'react-router'
import { NotFoundPage } from './pages/NotFoundPage'
import { OrderPage } from './pages/OrderPage'
import { ShopPage } from './pages/ShopPage'
import { UserOrdersPage } from './pages/UserOrdersPage'

/**
 * Client-side routes: the browser shows these pages without asking a server for them. Opening one
 * directly (a reload, a bookmark) still asks the server for its path, which must answer with
 * index.html: Vite's dev server does, and nginx's fallback will (TS-015).
 */
export function App() {
  return (
    <>
      <header className="site-header">
        <h1>
          <Link to="/">TinyShop</Link>
        </h1>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<ShopPage />} />
          <Route path="/orders/:id" element={<OrderPage />} />
          <Route path="/users/:id/orders" element={<UserOrdersPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </main>
    </>
  )
}
