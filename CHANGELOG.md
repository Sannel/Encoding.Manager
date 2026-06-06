# Changelog

## [0.0.1] - 2026-06-04
- Initial release of the Sannel Encoding Manager project.
- Included standard documentation (README, LICENSE, CODE_OF_CONDUCT, SECURITY).
- Multi-angle disc track support: when a disc title has more than one angle, the runner now produces one output file per angle (e.g. `Movie Title - a1.mkv`, `Movie Title - a2.mkv`). Single-angle titles are encoded as before with no suffix. Angle count is parsed automatically from the HandBrake scan JSON (`AngleCount` field).
