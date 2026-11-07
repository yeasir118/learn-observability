import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { toApiError, type ApiError } from '../../api/client'
import { getUser } from '../../api/tinyshop'
import type { User } from '../../api/types'
import { ErrorPanel } from '../../components/ErrorPanel'

type Lookup =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'found'; user: User }
  | { status: 'error'; error: ApiError }

/** GET /api/users/{id}, on demand. */
export function UserLookup() {
  const [userId, setUserId] = useState('')
  const [lookup, setLookup] = useState<Lookup>({ status: 'idle' })

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setLookup({ status: 'loading' })
    try {
      setLookup({ status: 'found', user: await getUser(userId.trim()) })
    } catch (error) {
      setLookup({ status: 'error', error: toApiError(error) })
    }
  }

  return (
    <>
      <form className="inline-form" aria-label="Find a user" onSubmit={handleSubmit}>
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
        <button type="submit" disabled={lookup.status === 'loading'}>
          Look up
        </button>
      </form>

      {lookup.status === 'loading' && <p className="muted">Looking up…</p>}
      {lookup.status === 'found' && (
        <p>
          <strong>{lookup.user.name}</strong> (user {lookup.user.id}) ·{' '}
          <Link to={`/users/${lookup.user.id}/orders`}>view orders</Link>
        </p>
      )}
      {lookup.status === 'error' && <ErrorPanel error={lookup.error} />}
    </>
  )
}
