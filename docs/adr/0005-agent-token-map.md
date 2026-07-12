# Consumer Flag takes Agent Tokens from the vercel-labs/skills table; default destination is `.agents/skills/`

The Consumer Flag value is a semicolon list of Agent Tokens using the `--agent` ids from the [vercel-labs/skills supported-agents table](https://github.com/vercel-labs/skills#supported-agents) (matched case-insensitively). Each token maps to that agent's documented project path. Verified fact driving this: Claude Code reads only `.claude/skills/` — it does NOT scan the `.agents/skills/` cross-vendor convention — so a single destination cannot serve all agents.

V1 curated map baked into the Consumer Targets:

| Token | Destination | Variant |
|---|---|---|
| `universal` (default), `opencode`, `codex`, `cursor`, `gemini-cli`, `github-copilot`, `amp`, `cline`, `zed`, `warp` | `.agents/skills/` | `agents` |
| `claude-code` | `.claude/skills/` | `claude` |
| token containing `/` or `\` | treated as a custom destination dir (relative to Workspace Root) | `agents` |

Unknown tokens produce a build warning and are skipped. Destinations are deduplicated before copy. Extending the map is a table edit, not an architecture change.
