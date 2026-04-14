# `.claude` Structure

This folder now follows a simple rule:

- `agents/` = execution personas that own a full task end to end
- `skills/` = narrow playbooks, guardrails, and reference packs for a specific domain

## When To Use An Agent

Use an agent when the work needs a stable operating mode across many files or steps.

Good agent use cases:

- Implement a feature or bug fix
- Perform a code review and produce findings
- Own a longer task that needs judgment, sequencing, and follow-through

Agent responsibilities:

- Decide what to inspect first
- Execute the task
- Keep quality bar and output style consistent

## When To Use A Skill

Use a skill when the model needs domain-specific rules or a reusable checklist.

Good skill use cases:

- Security audit checklist
- Architecture invariants
- DevOps and Docker patterns
- Git workflow and PR rules

Skill responsibilities:

- Define what good looks like in one area
- Provide repo-specific constraints and workflows
- Stay small and composable

## Current Separation

### Agents

- `code-reviewer.md`
  Owns review execution and reporting.
- `dotnet-core-expert.md`
  Owns implementation tasks in the .NET codebase.

### Skills

- `code-review/`
  Review checklist and review rules that support the reviewer agent.
- `devops/`
  Docker, compose, CI/CD, and secrets workflow.
- `dotnet-core-expert/`
  Repo-specific implementation guardrails that support the .NET agent.
- `git-workflow/`
  Branching, commit, and PR conventions.
- `principal-architect/`
  Non-negotiable architecture and domain invariants.
- `security-auditor/`
  Security audit scope, file targets, and reporting format.

## Rule For Overlap

If something needs both an agent and a skill:

- the agent owns execution
- the skill provides constraints

In other words:

- agent = "who does the work"
- skill = "how this kind of work should be done here"

## What Should Not Be Duplicated

Do not repeat the same long checklist in both an agent and a skill.

Prefer this split:

- agent files describe task ownership, operating mode, and output style
- skill files describe domain rules, repo invariants, and targeted workflows
