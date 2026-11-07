// Display only. Amounts arrive as integer cents and are never used for arithmetic here: totals
// are computed by the server (contracts.md §1.2).

const usd = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

/** 10000 → "$100.00" */
export function formatCents(cents: number): string {
  return usd.format(cents / 100)
}

/** An RFC 3339 UTC timestamp in the browser's local time zone. */
export function formatTimestamp(timestamp: string): string {
  return new Date(timestamp).toLocaleString()
}
