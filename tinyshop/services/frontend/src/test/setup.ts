// Runs before every test file: adds DOM matchers such as toBeInTheDocument, and unmounts what a
// test rendered once it ends.
import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

afterEach(() => cleanup())
