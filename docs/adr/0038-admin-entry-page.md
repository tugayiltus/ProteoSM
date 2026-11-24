# ADR-0038: Own Entry Page for the Admin Area

**Date**: 2026-11-24
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

ADR-0003 put the user application and `/admin` in one single-page application. The owner wants the admin interface separate from daily work. Session, CSRF, API and Astryx should stay shared, and no right should change because a screen moves.

## Decision

- One Vite build with two entries: `index.html` (user application) and `admin/index.html` (admin) on the same origin (`a7096a1`). This replaces the single-entry part of ADR-0003; the same-origin part stays.
- The API serves the admin entry for `/admin` and every page below it. Links between the areas load the page (`AreaLink`, `LeaveArea`). Sign-in stays in the user application, with the return page in the `from` parameter, local paths only.
- Shared code lives in `src/shared` or in the owning feature. ESLint forbids imports from the admin folder outside the admin entry.
- Users, teams, sign-in method, SLA, email, system, audit and service definitions belong to `/admin`. Approvals, the service choice and the impact view stay in the user application. Every policy stays on the server as before.

## Alternatives Considered

### Alternative 1: Separate build
- **Pros**: Independent bundle and release.
- **Cons**: Shared code downloads twice; a second build in CI and the image.
- **Why not**: Both areas release together, and the shared chunks are most of the size.

### Alternative 2: Separate origin or private network
- **Pros**: The admin pages can be closed at the network level.
- **Cons**: Cookie, CSRF and CORS work; `/api/v1/admin/*` must be closed too, or nothing is gained.
- **Why not**: No such need exists today.

### Alternative 3: Keep one entry
- **Pros**: No page load between the areas.
- **Cons**: The areas stay one application.
- **Why not**: The owner wants them separate.

## Consequences

### Positive
- Clear boundary between administration and daily work, checked by lint.
- One deployment, one session and one API as before.

### Negative
- Going from one area to the other loads the page, so the breadcrumb return does not cross areas.
- The separation is not a security boundary; the server policies are.

### Risks
- A server that serves the wrong entry for a path would reload forever. Mitigation: `EntryPageTests` and `areas.spec.ts`.
