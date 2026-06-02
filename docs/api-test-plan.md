# API Endpoint Test Plan

## What was already verified

| Check | Result |
|---|---|
| `dotnet build` | ✅ 0 errors, 0 warnings |
| Unauthenticated GET to all 5 controllers | ✅ All 302 → routes resolve, middleware wired |
| Login as `admin@admin.com` | ✅ Auth cookie issued |
| Authenticated JSON responses | ❌ Not tested (tmpfs ran out of space mid-session) |

---

## One-time setup

```bash
# Start the app
dotnet run --urls "http://localhost:5050"

# Login and save cookie jar
curl -s -c cookies.txt http://localhost:5050/Identity/Account/Login -o login.html

TOKEN=$(grep -o 'name="__RequestVerificationToken" type="hidden" value="[^"]*"' login.html \
  | sed 's/.*value="\([^"]*\)".*/\1/')

curl -s -c cookies.txt -b cookies.txt \
  -X POST http://localhost:5050/Identity/Account/Login \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "Input.Email=admin%40admin.com&Input.Password=Password123%21&Input.RememberMe=false&__RequestVerificationToken=${TOKEN}" \
  -o /dev/null -w "%{http_code}"
# Expected: 302

# All subsequent calls use: -b cookies.txt
```

**Seed data that exists out of the box:**
- Project ID `10` ("Extras"), work packages `SICK`, `VACN`, `SHOL`, `FLEX`
- Admin user `admin@admin.com` — roles Admin/Supervisor/HR, PM of project 10

---

## Employees `/api/employees`

### GET /api/employees
No prerequisites.
```bash
curl -b cookies.txt "http://localhost:5050/api/employees?page=1&pageSize=10"
# Expected: 200 — { users:[...], totalPages, currentPage, pageSize }
```

### GET /api/employees/{id}
Prerequisite: grab `<ADMIN_ID>` from the list above.
```bash
curl -b cookies.txt "http://localhost:5050/api/employees/<ADMIN_ID>"
# Expected: 200 — admin user object
```

### PUT /api/employees/{id}
Prerequisite: `<ADMIN_ID>` from list.
```bash
curl -b cookies.txt -X PUT "http://localhost:5050/api/employees/<ADMIN_ID>" \
  -H "Content-Type: application/json" \
  -d '{"id":"<ADMIN_ID>","firstName":"admin","lastName":"admin","jobTitle":"System Admin","email":"admin@admin.com","userName":"admin@admin.com"}'
# Expected: 204
```

### GET /api/employees/timesheet-approvers
Prerequisite: `<ADMIN_ID>`.
```bash
curl -b cookies.txt "http://localhost:5050/api/employees/timesheet-approvers?supervisorId=<ADMIN_ID>"
# Expected: 200 — array (may be empty if no supervised users)
```

### GET /api/employees/available-for-project
No extra prerequisites (project 10 exists).
```bash
curl -b cookies.txt "http://localhost:5050/api/employees/available-for-project?projectId=10"
# Expected: 200 — array of users not yet on project 10
```

### POST /api/employees/add-to-project
Prerequisite: `<USER_ID>` of a user not already on the project (e.g. hr1@hr.com from seed).
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/employees/add-to-project" \
  -H "Content-Type: application/json" \
  -d '[{"userId":"<USER_ID>","projectId":10}]'
# Expected: 200 — echo of the posted array
```

### POST /api/employees/assign-tsa
Prerequisite: current user must supervise `<USER_ID>` (admin supervises all HR seed users).
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/employees/assign-tsa" \
  -H "Content-Type: application/json" \
  -d '{"userId":"<USER_ID>","projectId":10}'
# Expected: 200
```

---

## Projects `/api/projects`

### GET /api/projects
No prerequisites.
```bash
curl -b cookies.txt "http://localhost:5050/api/projects"
# Expected: 200 — array containing at least project 10
```

### POST /api/projects
Prerequisite: must be HR or Admin. Use a fresh project ID that doesn't exist yet.
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/projects" \
  -H "Content-Type: application/json" \
  -d '{
    "project": {
      "projectId": 99,
      "projectTitle": "Test Project",
      "projectManagerId": "<ADMIN_ID>"
    },
    "budgets": []
  }'
# Expected: 200
```

### GET /api/projects/{id}/employees
Prerequisite: project exists.
```bash
curl -b cookies.txt "http://localhost:5050/api/projects/10/employees"
# Expected: 200 — array of employees on project 10
```

### GET /api/projects/{id}/pm
Prerequisite: caller must be PM of the project.
```bash
curl -b cookies.txt "http://localhost:5050/api/projects/10/pm"
# Expected: 200 — admin user ID string
```

### POST /api/projects/{id}/asm
Prerequisite: project 99 created above; `<EMPLOYEE_NUMBER>` is the integer employee number (not the GUID) of the user to assign.
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/projects/99/asm" \
  -H "Content-Type: application/json" \
  -d '"<EMPLOYEE_NUMBER>"'
# Expected: 200
```

### GET /api/projects/{id}/report, /week-report, /pcbac
Prerequisite: project exists; may return 400 if no timesheet data exists yet for the project.
```bash
curl -b cookies.txt "http://localhost:5050/api/projects/10/report"       # Expected: 200 PDF or 400
curl -b cookies.txt "http://localhost:5050/api/projects/10/week-report"  # Expected: 200 PDF or 400
curl -b cookies.txt "http://localhost:5050/api/projects/10/pcbac"        # Expected: 200 PDF or 400
```

### POST /api/projects/{id}/close
**Do this last — closing is irreversible. Do not close project 10.**
Prerequisite: project 99 created above; caller must be PM.
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/projects/99/close"
# Expected: 200
```

---

## Work Packages `/api/workpackages`

### GET /api/workpackages/responsible
No prerequisites.
```bash
curl -b cookies.txt "http://localhost:5050/api/workpackages/responsible"
# Expected: 200 — array of WPs where current user is responsible engineer (may be empty)
```

### GET /api/workpackages/{id}
Work packages `SICK`, `VACN`, `SHOL`, `FLEX` exist from seed.
```bash
curl -b cookies.txt "http://localhost:5050/api/workpackages/SICK"
# Expected: 200 — work package object
```

### GET /api/workpackages/project/{projectId}/tree
Prerequisite: caller must be PM.
```bash
curl -b cookies.txt "http://localhost:5050/api/workpackages/project/10/tree"
# Expected: 200 — array of WPs with budget totals
```

### POST /api/workpackages/project/{projectId}/split
Prerequisite: project 99 exists; a root WP must exist in that project first (create one via the MVC UI or seed). Body uses `WorkPackageViewModel`.
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/workpackages/project/99/split" \
  -H "Content-Type: application/json" \
  -d '{
    "WorkPackage": {
      "workPackageId": "A",
      "parentWorkPackageId": "0",
      "title": "Phase A"
    },
    "budgets": []
  }'
# Expected: 200 — new child work package object
```

### POST /api/workpackages/project/{projectId}/budget-details
Prerequisite: WP exists in the project.
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/workpackages/project/10/budget-details" \
  -H "Content-Type: application/json" \
  -d '{"workPackageId":"SICK"}'
# Expected: 200 — { pmBudgets:[...], reBudgets:[...] }
```

### POST /api/workpackages/project/{projectId}/wp-employees
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/workpackages/project/10/wp-employees" \
  -H "Content-Type: application/json" \
  -d '{"workPackageId":"SICK"}'
# Expected: 200 — array of assigned employees
```

### POST /api/workpackages/project/{projectId}/candidate-employees
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/workpackages/project/10/candidate-employees" \
  -H "Content-Type: application/json" \
  -d '{"workPackageId":"SICK"}'
# Expected: 200 — array of employees eligible to be assigned
```

### POST /api/workpackages/project/{projectId}/assigned-employees
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/workpackages/project/10/assigned-employees" \
  -H "Content-Type: application/json" \
  -d '{"workPackageId":"SICK"}'
# Expected: 200 — array
```

### POST /api/workpackages/project/{projectId}/assign-employees
Prerequisite: `<USER_ID>` must be on the project and eligible.
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/workpackages/project/10/assign-employees" \
  -H "Content-Type: application/json" \
  -d '[{"userId":"<USER_ID>","workPackageId":"SICK","workPackageProjectId":10}]'
# Expected: 200
```

### POST /api/workpackages/project/{projectId}/assign-re
Prerequisite: `<USER_ID>` must be assigned to the WP.
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/workpackages/project/10/assign-re" \
  -H "Content-Type: application/json" \
  -d '{"userId":"<USER_ID>","workPackageId":"SICK","workPackageProjectId":10}'
# Expected: 200 — result string
```

### POST /api/workpackages/budgets-and-estimates
Prerequisite: current user must be responsible engineer on a lowest-level WP.
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/workpackages/budgets-and-estimates" \
  -H "Content-Type: application/json" \
  -d '{"budgets":[],"estimates":[]}'
# Expected: 200 (or 400 if validation fails with empty arrays)
```

### POST /api/workpackages/project/{projectId}/close-wp
**Irreversible. Test on a WP you created, not SICK/VACN/SHOL/FLEX.**
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/workpackages/project/99/close-wp" \
  -H "Content-Type: application/json" \
  -d '{"workPackageId":"A"}'
# Expected: 200
```

---

## Timesheets `/api/timesheets`

### GET /api/timesheets/unapproved
No prerequisites.
```bash
curl -b cookies.txt "http://localhost:5050/api/timesheets/unapproved"
# Expected: 200 — array of unapproved timesheets for current user
```

### GET /api/timesheets/approved
```bash
curl -b cookies.txt "http://localhost:5050/api/timesheets/approved"
# Expected: 200 — array
```

### GET /api/timesheets/to-approve
Prerequisite: current user must be a timesheet approver for at least one other user.
```bash
curl -b cookies.txt "http://localhost:5050/api/timesheets/to-approve"
# Expected: 200 — array (may be empty)
```

### POST /api/timesheets
Creates a timesheet for the week containing the given date.
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/timesheets" \
  -H "Content-Type: application/json" \
  -d '"2026-06-02"'
# Expected: 200 — timesheet object with TimesheetId, EndDate, TotalHours
# Expected: 400 if a timesheet for that week already exists
```

### POST /api/timesheets/get
Prerequisite: `<TIMESHEET_ID>` from the create call or unapproved list.
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/timesheets/get" \
  -H "Content-Type: application/json" \
  -d '"<TIMESHEET_ID>"'
# Expected: 200 — array of timesheet row DTOs
```

### POST /api/timesheets/rows/update
Prerequisite: `<TIMESHEET_ID>` and `<ROW_ID>` from the rows returned above.
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/timesheets/rows/update" \
  -H "Content-Type: application/json" \
  -d '{
    "timesheetRowId": <ROW_ID>,
    "timesheetId": <TIMESHEET_ID>,
    "saturday": 0, "sunday": 0,
    "monday": 8, "tuesday": 8, "wednesday": 8, "thursday": 8, "friday": 8
  }'
# Expected: 200 — updated row or 400 with validation errors
```

### POST /api/timesheets/rows/custom
Prerequisite: `<TIMESHEET_ID>` must belong to current user and be unapproved. `type` is one of `"SICK"`, `"VACN"`, `"SHOL"`, `"FLEX"`.
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/timesheets/rows/custom" \
  -H "Content-Type: application/json" \
  -d '{"timesheetId":"<TIMESHEET_ID>","type":"SICK"}'
# Expected: 200 — new row object
```

### POST /api/timesheets/submit
Prerequisite: timesheet must be unsubmitted. Password is the user's login password.
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/timesheets/submit" \
  -H "Content-Type: application/json" \
  -d '{"timesheet":<TIMESHEET_ID>,"password":"Password123!","flexhours":0,"overtime":0}'
# Expected: 200 — array of row DTOs with updated state
```

### POST /api/timesheets/approve
Prerequisite: timesheet must be submitted; caller must be the assigned timesheet approver.
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/timesheets/approve" \
  -H "Content-Type: application/json" \
  -d '{"timesheet":<TIMESHEET_ID>,"password":"Password123!"}'
# Expected: 200
```

### POST /api/timesheets/decline
Prerequisite: same as approve — timesheet submitted, caller is approver.
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/timesheets/decline" \
  -H "Content-Type: application/json" \
  -d '{"timesheet":<TIMESHEET_ID>,"password":"Password123!","approverNotes":"Missing hours on Monday."}'
# Expected: 200
```

---

## Notifications `/api/notifications`

### GET /api/notifications
No prerequisites.
```bash
curl -b cookies.txt "http://localhost:5050/api/notifications"
# Expected: 200 — array of notifications (may be empty)
```

### POST /api/notifications/dismiss
Prerequisite: `<NOTIFICATION_ID>` from the list above.
```bash
curl -b cookies.txt -X POST "http://localhost:5050/api/notifications/dismiss" \
  -H "Content-Type: application/json" \
  -d '"<NOTIFICATION_ID>"'
# Expected: 200
```
