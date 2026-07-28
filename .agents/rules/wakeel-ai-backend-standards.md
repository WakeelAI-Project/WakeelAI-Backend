---
trigger: always_on
---

You are working on the Wakeel AI Backend project.

This project follows Clean Architecture.

The solution contains:

- Wakeel.API
- Wakeel.Application
- Wakeel.Domain
- Wakeel.Infrastructure

Always follow these architectural rules.

Architecture Rules

- Domain must never depend on any other project.
- Application can depend only on Domain.
- Infrastructure implements interfaces from Application.
- API references Application and Infrastructure only.
- Never place business logic inside Controllers.
- Never access DbContext from API.
- Never reference Infrastructure from Domain.
- Prefer dependency injection.
- Keep methods small and focused.
- Follow SOLID principles.

Coding Rules

- Use file-scoped namespaces.
- One public class per file.
- Use meaningful names.
- Avoid unnecessary comments.
- Write readable code before clever code.
- Use async/await for I/O operations.

Git Rules

- Base branch is develop.
- Never work directly on main.
- One feature branch per task.
- Never generate git commands unless requested.

Agent Behavior

Before writing code:

1. Read the existing code.
2. Explain your plan.
3. List every file that will change.
4. Wait for approval.

Never modify files without explaining why .

If information is missing,
ask questions instead of making assumptions.
Don't assume anything is unclear; you must ask me first.
When reviewing code,

separate your response into:

Facts

Assumptions

Potential Issues

Recommendations

If you are not sure,

say

"I am not certain."

instead of inventing an answer.

Teaching Mode

When explaining code:

- Explain why, not only what.
- Explain the architecture behind every decision.
- Do not assume I know Clean Architecture.
- Teach concepts step by step.
- If there are multiple solutions, explain the trade-offs.
- Prefer education over speed.
- Never skip reasoning.