# Changelog

## [0.0.1] - 2026-06-04
- Initial release of the Sannel Encoding Manager project.
- Included standard documentation (README, LICENSE, CODE_OF_CONDUCT, SECURITY).
- Multi-angle disc track support: when a disc title has more than one angle, the runner now produces one output file per angle (e.g. `Movie Title - a1.mkv`, `Movie Title - a2.mkv`). Single-angle titles are encoded as before with no suffix. Angle count is parsed automatically from the HandBrake scan JSON (`AngleCount` field).
- MCP server at `/mcp` so AI assistants can browse media roots, scan discs, look up TVDB/OMDb names and queue encode jobs (16 tools). Per-user API keys (hash-only storage, shown once) managed on the new **AI Access** page, which also shows client setup snippets. Forced rescans/refreshes are rate-limited per key.
- Disc menu inspection (`inspect_disc_menus`): an isolated probe maps every DVD/Blu-ray menu button to a submenu or a title/chapter range (libdvdnav exact for DVD, libbluray best-effort for Blu-ray, BD-J with `DiscMenu:JavaHome`) and returns annotated libvlc screenshots. Results are cached in the `DiscMenuCache` table.
- Queue items record who created them and whether via the UI or MCP (`CreatedBy`, `CreatedByObjectId`, `CreatedVia`), shown in the queue detail dialog.
- "Copy for AI" button in the Filesystem Browser copies an item's root, path and suggested MCP steps.
- New `Mcp` and `DiscMenu` configuration sections; `AddMcpSupport` migration for SQLite and PostgreSQL.
- Disc menu fixes from the first real-disc runs: BD-J menus are detected from their graphics overlay (they never raise the HDMV menu event); the JRE's `bin` folders are put on the probe's PATH on Windows so `jvm.dll` loads; Blu-ray and screenshot failures now report the actual reason; DVD screenshots no longer give up when libvlc never reports a menu title; relative `DiscMenu:OutputPath` defaults under `%ProgramData%\SannelEncodingManager` on Windows. Probe version bumped to 2 so cached menu maps are rebuilt.
