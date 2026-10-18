# Contributing

FiveVibe is early and practical contributions are welcome: reproducible game bugs, tested approach points, accessible UI improvements, and small new native actions.

Build on Windows using scripts/Get-Dependencies.ps1 and scripts/Build.ps1. Run tests/Test-ChatUpdate.ps1 after changing installation code. No API key is required for automated tests. Live API probes are optional and billed to your own API project.

Before opening a pull request:

- Explain the concrete request or gameplay trigger, resulting behavior and validation.
- Keep GTA calls on the script thread. HTTP workers may only publish data or enqueue commands.
- Preserve F8, browser Stop, cancellation generations and configuration during updates.
- Validate custom data and AI output before execution. Never accept executable code or invented coordinates.
- Add meaningful tests for behavior or security boundaries that change.
- Clearly separate automated checks from actual in-game observations. Include game, Script Hook and SHVDNE versions when reporting gameplay results.
- Do not commit credentials, local install records, logs, binaries, savegames or proprietary assets.

The UI and documentation are English. English and Italian requests are supported; additional local-parser aliases are welcome.

Contributions are provided under the project's MIT license.
