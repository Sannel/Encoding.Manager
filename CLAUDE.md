# Sannel Encoding Manager - Claude Instructions

The project conventions are shared with GitHub Copilot and live in `.github/copilot-instructions.md`. Keep that file as the single source of truth — update it rather than duplicating content here.

@.github/copilot-instructions.md

## Claude-Specific Notes

- The "File Editing" rules above refer to Copilot's `replace_string_in_file` tool. For Claude, the equivalent is the **Edit** tool: when an edit fails, re-read the exact surrounding lines to get the literal whitespace (tabs, CRLF) and retry with Edit — do NOT fall back to `sed`, `awk`, or shell scripts to modify files.
- The scripting constraint applies to Bash usage too: never invoke `python`/`python3`; use `pwsh` for any non-trivial scripting.
- Feature planning: use the `/feature-planner` skill (`.claude/skills/feature-planner/SKILL.md`), which mirrors `.github/agents/feature-planner.agent.md`. Keep the two in sync when either changes.
