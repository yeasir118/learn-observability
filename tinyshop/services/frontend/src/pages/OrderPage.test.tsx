import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { describe, expect, it } from 'vitest'
import type { Order } from '../api/types'
import { json, mockFetch, problem } from '../test/fetch'
import { OrderPage } from './OrderPage'

const paid: Order = {
  id: 1001,
  userId: 123,
  productId: 2,
  productName: 'Keyboard',
  unitPriceCents: 10000,
  quantity: 2,
  totalCents: 20000,
  status: 'PAID',
  failureReason: null,
  transactionId: 'txn_8f14e45f',
  createdAt: '2026-10-18T14:30:05.123Z',
  updatedAt: '2026-10-18T14:30:05.187Z',
}

function renderOrderPage(id: string) {
  render(
    <MemoryRouter initialEntries={[`/orders/${id}`]}>
      <Routes>
        <Route path="/orders/:id" element={<OrderPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('OrderPage', () => {
  it('shows a paid order with its transaction', async () => {
    mockFetch({ 'GET /api/orders/1001': () => json(paid) })

    renderOrderPage('1001')

    expect(await screen.findByText('Paid')).toBeInTheDocument()
    expect(screen.getByText('txn_8f14e45f')).toBeInTheDocument()
    expect(screen.getByText('$200.00')).toBeInTheDocument()
  })

  it('shows a declined order with its reason', async () => {
    // Payment only declines once TS-016's fault switches exist, so this is the declined path's check.
    mockFetch({
      'GET /api/orders/1002': () =>
        json({ ...paid, id: 1002, status: 'FAILED', transactionId: null, failureReason: 'insufficient_funds' }),
    })

    renderOrderPage('1002')

    expect(await screen.findByText('Failed')).toBeInTheDocument()
    expect(screen.getByText('insufficient_funds')).toBeInTheDocument()
  })

  it('shows an unknown order as an error with its trace ID', async () => {
    mockFetch({
      'GET /api/orders/9999': () =>
        problem(404, { title: 'Order not found', code: 'order_not_found', traceId: '00-abc-def-01' }),
    })

    renderOrderPage('9999')

    expect(await screen.findByRole('alert')).toHaveTextContent('Order not found')
    expect(screen.getByText('order_not_found')).toBeInTheDocument()
    expect(screen.getByText('00-abc-def-01')).toBeInTheDocument()
  })
})
