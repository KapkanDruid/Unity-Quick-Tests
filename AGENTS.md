# AGENTS.md

## Project Overview

This repository is a Unity UPM package for Quick Editor Tests.

## Rules

- Keep package code independent from game-specific services.
- Runtime code must not depend on `UnityEditor`.
- Editor-only behavior belongs in `Editor/` assemblies.
- Preserve a simple user-facing API: attributes on parameterless methods.
- Do not add IL postprocessing until the registry-based prototype has been validated.

## Validation

Prefer focused compile checks against the target Unity editor assemblies before changing package behavior.
