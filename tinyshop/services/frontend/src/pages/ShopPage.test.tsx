import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useParams } from 'react-router'
import { describe, expect, it } from 'vitest'
import { json, mockFetch, problem } from '../test/fetch'
import { ShopPage } from './ShopPage'

const products = {
  items: [
    { id: 1, name: 'Laptop', priceCents: 100000 },
    { id: 2, name: 'Keyboard', priceCents: 10000 },
  ],
}

function OrderStub() {
  return <p>Order page {useParams().id}</p>
}

function renderShop() {
  render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route path="/" element={<ShopPage />} />
        <Route path="/orders/:id" element={<OrderStub />} />
      </Routes>
    </MemoryRouter>,
  )
}

/** Fills in the order form (it appears once the products have loaded). */
async function fillOrderForm(fields: { userId: string; productId?: string; quantity: string }) {
  const user = userEvent.setup()
  const form = within(await screen.findByRole('form', { name: 'Place an order' }))
  await user.type(form.getByLabelText('User ID'), fields.userId)
  if (fields.productId) {
    await user.selectOptions(form.getByLabelText('Product'), fields.productId)
  }
  await user.clear(form.getByLabelText('Quantity'))
  await user.type(form.getByLabelText('Quantity'), fields.quantity)
  await user.click(form.getByRole('button', { name: 'Place order' }))
  return form
}

describe('ShopPage', () => {
  it('lists the products with their prices', async () => {
    mockFetch({ 'GET /api/products': () => json(products) })

    renderShop()

    expect(await screen.findByRole('cell', { name: 'Laptop' })).toBeInTheDocument()
    expect(screen.getByRole('cell', { name: '$1,000.00' })).toBeInTheDocument()
  })

  it('places an order with numbers in the body, then opens it', async () => {
    const fetchMock = mockFetch({
      'GET /api/products': () => json(products),
      'POST /api/orders': () => json({ id: 1001 }, 201),
    })
    renderShop()

    await fillOrderForm({ userId: '123', productId: '2', quantity: '2' })

    expect(await screen.findByText('Order page 1001')).toBeInTheDocument()
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({ userId: 123, productId: 2, quantity: 2 })
  })

  it("shows the server's validation errors and stays on the form", async () => {
    mockFetch({
      'GET /api/products': () => json(products),
      'POST /api/orders': () =>
        problem(400, {
          title: 'Validation failed',
          code: 'validation_failed',
          errors: { quantity: ['Must be between 1 and 100.'] },
        }),
    })
    renderShop()

    const form = await fillOrderForm({ userId: '123', quantity: '0' })

    expect(await form.findByRole('alert')).toHaveTextContent('Must be between 1 and 100.')
    expect(form.getByRole('button', { name: 'Place order' })).toBeEnabled()
  })

  it('shows a user that was looked up, with a link to their orders', async () => {
    mockFetch({
      'GET /api/products': () => json(products),
      'GET /api/users/123': () => json({ id: 123, name: 'Alice' }),
    })
    const user = userEvent.setup()
    renderShop()

    const lookup = within(screen.getByRole('form', { name: 'Find a user' }))
    await user.type(lookup.getByLabelText('User ID'), '123')
    await user.click(lookup.getByRole('button', { name: 'Look up' }))

    expect(await screen.findByText('Alice')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'view orders' })).toHaveAttribute('href', '/users/123/orders')
  })
})
