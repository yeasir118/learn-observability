import { describe, expect, it } from 'vitest'
import { formatCents } from './format'

describe('formatCents', () => {
  it.each([
    [10000, '$100.00'],
    [5, '$0.05'],
    [123456789, '$1,234,567.89'],
    [0, '$0.00'],
  ])('formats %i cents as %s', (cents, expected) => {
    expect(formatCents(cents)).toBe(expected)
  })
})
