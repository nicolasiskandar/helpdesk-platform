import { defineConfig, globalIgnores } from "eslint/config"
import nextVitals from "eslint-config-next/core-web-vitals"
import nextTs from "eslint-config-next/typescript"

export default defineConfig([
  ...nextVitals,
  ...nextTs,
  {
    rules: {
      // Data fetching / auth restore happens inside effects everywhere in this
      // codebase (lib/store.tsx, lib/signalr.ts, page loaders). The
      // react-hooks v6 rule demands a different effect-free pattern; migrating
      // every loader is a separate refactor, so it stays off for now.
      "react-hooks/set-state-in-effect": "off",
      // The codebase predates strict `unknown` error handling; dozens of catch
      // handlers use `catch (err: any)` to read err.status/err.message. Keep
      // the rule off until those are migrated to typed error helpers.
      "@typescript-eslint/no-explicit-any": "off",
    },
  },
  globalIgnores([".next/**", "node_modules/**", "next-env.d.ts"]),
])
