import { Link, useLocation } from 'react-router'

/** Any path the app has no page for. The server never sees it: routing happens in the browser. */
export function NotFoundPage() {
  const { pathname } = useLocation()

  return (
    <section>
      <h2>Page not found</h2>
      <p>
        There's no page at <code>{pathname}</code>. <Link to="/">Back to the shop</Link>.
      </p>
    </section>
  )
}
