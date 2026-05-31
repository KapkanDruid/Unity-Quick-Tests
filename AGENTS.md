# AGENTS.md

## Project Overview

This repository is a Unity UPM package for Unity Quick Tests.

## Knowledge Loading

Before broad scans, use this order when the local files exist:

1. Read `.agent-memory/README.md`.
2. Query `graphify-out/graph.json` with Graphify for the relevant area.
3. Read only the relevant curated-memory page.
4. Read `README.md` or `Docs/DESIGN.md` when human-facing context is needed.
5. Open the required source files.

`graphify-out/` and `.agent-memory/` are local-only and gitignored. Generated graph
artifacts must not be edited manually.

## Rules

- Keep package code independent from game-specific services.
- Runtime code must not depend on `UnityEditor`.
- Editor-only behavior belongs in `Editor/` assemblies.
- Preserve a simple user-facing API: attributes on parameterless methods.
- Do not add IL postprocessing until the registry-based prototype has been validated.

## Validation

Prefer focused compile checks against the target Unity editor assemblies before changing package behavior.
