# SHEET Backend

See `../CLAUDE.md` for project overview, architecture, and cross-cutting docs.
See `../docs/gotchas.md` and `../docs/domain-notes.md` before debugging business logic.

## Quick Reference

**Run:** `dotnet run` → `http://localhost:5000`. Swagger at `/swagger`.  
**Seed:** `admin@admin.com` / `Password123!`. Project 10 ("Extras") always present.  
**API tests:** `docs/api-test-plan.md` — full curl test harness.

## Structure
```
Controllers/Api/        # REST endpoints — one controller per domain
Controllers/            # KeyRequirement.cs — auth gate
Services/               # Business logic — I*Service.cs + *Service.cs
Models/TimesheetModels/ # EF Core entities
Models/                 # ViewModels/DTOs (WorkPackageViewModel, TimesheetViewModel)
```

## Auth Gate
All API controllers require `[Authorize(Policy = "KeyRequirement")]`.
This checks `ApplicationUser.PublicKey != null` — new users without a key get 403.
The setup flow (`POST /api/auth/activate`) generates and stores the key pair.

## Common Debugging Patterns

**`Sequence contains no elements`:** Always caused by `.First()` on an empty LINQ result.
- Budget lookups: WPs may have no budget for a given labour grade (sparse budgets are valid)
- Fix: `.FirstOrDefault()` + `if (result == null) continue;`
- Grep for all `.First()` calls in the crashing service — fix them all at once

**Validation 400:** Check `ModelState.IsValid` in the controller, then `[Required]`/`[Range]` on the model.

**PK issues:** WorkPackage has composite PK `(WorkPackageId, ProjectId)`. Budget links via string key `"{projectId}~{workPackageId}"`, not a FK.

## Key Files
| File | What's in it |
|---|---|
| `Services/WorkPackageService.cs` | WP creation, budget deduction, tree calculation, employee assignment |
| `Services/TimesheetService.cs` | Row CRUD, custom rows (SICK/VACN/SHOL/FLEX), submit/approve logic |
| `Services/ProjectService.cs` | Project creation (seeds root WP "0"), PM verification |
| `Controllers/KeyRequirement.cs` | Auth gate handler |
| `Models/TimesheetModels/Budget.cs` | Budget entity — `WPProjectId` string key, `BudgetAmount = Days * People` |
| `Models/TimesheetViewModel.cs` | `CustomRowModel { Type: string, TimesheetId: int }` |
