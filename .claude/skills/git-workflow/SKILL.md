---
name: git-workflow
description: Best practices for Git branching, commit messages in C# / ASP.NET Core projects. You should not push an create pull request without following these guidelines.
---

# Git Workflow (Best Practices)

This repo follows a lightweight GitFlow-style workflow:

- `main` = production-ready, always stable
- `develop` (optional) = integration branch for larger teams/releases
- short-lived feature branches = all work happens here
- pull requests required (even for small changes, when possible), but must wait for user approval to create a PR

If your team does not use `develop`, merge feature branches directly into `main`.

---

## Branching Strategy

### Branch naming

Use consistent prefixes:

- `feat/<short-description>` — new features
- `fix/<short-description>` — bug fixes
- `chore/<short-description>` — maintenance (deps, tooling)
- `docs/<short-description>` — documentation only
- `refactor/<short-description>` — refactors, no behavior change intended
- `test/<short-description>` — test additions/changes only
- `perf/<short-description>` — performance improvements
- `ci/<short-description>` — pipeline changes

Examples:

- `feat/user-auth`
- `fix/login-redirect`
- `chore/bump-dotnet-sdk`

### Work from the right base

- Branch off `main` for small teams (recommended)
- Branch off `develop` only if you run coordinated release trains

---

## Commit Message Standard (Semantic / Conventional Commits)

All commits MUST follow this format:

`<type>(<scope>): <subject>`

Where:

- **type** is required
- **scope** is optional but recommended (module/package/area)
- **subject** is required, imperative, short, no trailing period

### Allowed types

- `feat` — new user-facing feature
- `fix` — bug fix
- `docs` — documentation only
- `style` — formatting only (no logic change)
- `refactor` — code change that neither fixes a bug nor adds a feature
- `perf` — performance improvement
- `test` — add or fix tests
- `build` — build system changes (deps, bundlers, tooling)
- `ci` — CI configuration changes
- `chore` — other changes that don’t modify src or tests
- `revert` — revert a previous commit

### Examples

- `feat(auth): add refresh token rotation`
- `fix(api): handle null payload for webhook`
- `refactor(ui): extract modal into reusable component`
- `docs(readme): add local setup steps`
- `chore(deps): bump Newtonsoft.Json to 13.0.3`
- `ci(github): cache nuget packages`

### Breaking changes

If a change is breaking, use:

- `feat!: ...` or `fix!: ...`
  and/or include in the commit body:
  `BREAKING CHANGE: <explain what changed and migration notes>`

Example:

- `feat(api)!: rename /v1/users endpoint to /v2/users`

---

## Local Workflow (Day-to-Day)

1. Sync your base branch:
   - `git checkout main`
   - `git pull --rebase`

2. Create a branch:
   - `git checkout -b feat/<short-description>`

3. Commit frequently, but keep commits meaningful:
   - one logical change per commit
   - keep commits buildable when possible

4. Keep your branch up to date:
   - `git fetch origin`
   - `git rebase origin/main` (preferred)
     Avoid merge commits unless necessary.

5. Push:
   - `git push -u origin <branch>`

---

## Pull Requests

### PR rules

- PR title should follow Conventional Commits (helps release notes):
  - `feat(auth): add SSO support`
- Small PRs are preferred (reviewable in < 30 minutes).
- Include:
  - what changed
  - why it changed
  - how to test
  - screenshots/videos for UI changes (if applicable)

### PR checklist (minimum)

- [ ] Builds successfully
- [ ] Tests added/updated
- [ ] Lint/format passes (or justified)
- [ ] No secrets committed
- [ ] Docs updated if behavior changed

### Review & merging

- Prefer **Squash merge** to keep history clean.
- Ensure the squash commit message follows Conventional Commits.
- Delete branch after merge.

---

## Rebase, Squash, and Fixups (Clean History)

### Interactive rebase before opening PR (recommended)

- `git rebase -i origin/main`

Use this to:

- squash noisy commits
- reword messages to Conventional Commits
- reorder commits to improve readability

### Fixup commits

When addressing review feedback:

- make a new commit like `fixup! feat(scope): ...`
- squash it during `git rebase -i` before merging

---

## Hotfixes

For urgent production fixes:

- branch from `main`: `fix/<short-description>`
- keep it minimal
- PR + review (expedited)
- merge to `main`
- if using `develop`, backport by merging/rebasing into `develop`

---

## Tags & Releases (Optional but Recommended)

If using semantic versioning:

- `vMAJOR.MINOR.PATCH` (e.g., `v1.4.2`)
- bump:
  - MAJOR: breaking change
  - MINOR: new feature (backward compatible)
  - PATCH: bug fix (backward compatible)

Automate changelogs/releases with tools like:

- semantic-release
- release-please
- conventional-changelog

---

## Do / Don’t

### Do

- write clear commit messages
- rebase frequently
- keep PRs small and focused
- document behavior changes

### Don’t

: breaking change

- MINOR: new feature (backward compatible)
- PATCH: bug fix (backward compatible)

Automate changelogs/releases with tools like:

- semantic-release
- release-please
- conventional-changelog

---

## Do / Don’t

### Do

- write clear commit messages
- rebase frequently
- keep PRs small and focused
- document behavior changes

### Don’t

- commit secrets or credentials
- push directly to `main`
- mix refactors + features + formatting in one PR
- use vague messages like “updates”, “fix stuff”
