# Dashboard HR Metrics Update

## Summary

This PR updates the `GET /api/dashboard/summary` endpoint to remove the obsolete AI Usage metric, introduce a highly actionable "Employees on Leave Today" metric, and replace the previously hardcoded Documents count with a real, optimized database count.

## Changes

### 1. Replace AI Usage

* Removed `HandbookUploaded` from the DTO.
* Removed the old AI Usage placeholder calculation.
* Added `EmployeesOnLeaveToday` to represent the workforce availability.

### 2. Employees on Leave Today

This metric provides immediate situational awareness to HR by calculating the number of employees missing from the office today.

* Only considers **Approved** leave requests.
* Checks if the current UTC date falls between `StartDate` and `EndDate` inclusive.
* Is scoped securely to the authenticated company.
* Counts unique employees using `.Select(lr => lr.EmployeeId).Distinct().Count()` to ensure employees with overlapping leave records are not counted twice.

### 3. Generated Documents Count

This metric now reflects actual platform usage.

* Removed the hardcoded document count placeholder (`0`).
* Count is now retrieved dynamically from the `GeneratedDocuments` table.
* Accurately scoped by the authenticated user's `CompanyId`.
* Uses a highly efficient database-level `CountAsync()` operation rather than loading records into application memory.

### 4. Repository Changes

To support the efficient document count, the following repository enhancements were made:

* Added a `Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)` signature to `IGenericRepository`.
* Implemented the `CountAsync` method natively in `GenericRepository` using EF Core's underlying `.CountAsync()`.

## Tests

The changes have been thoroughly tested to guarantee reliability:

* 49 unit tests passed.
* 58 integration tests passed.
* Build succeeded with 0 errors and 0 warnings.
* Dashboard integration tests were completely updated for the new metric.
* Company/tenant isolation was tested and verified for all metrics.
* Overlapping approved leave scenarios were tested to ensure unique employee counts.
* The generated document count was strictly tested for company isolation using multiple seeded companies.

## API Contract Change

**Removed:**
`handbook_uploaded`

**Added:**
`employees_on_leave_today`

**Final Response Example:**

```json
{
  "employee_count": 0,
  "active_employees": 0,
  "pending_leave_requests": 0,
  "employees_on_leave_today": 0,
  "generated_documents_count": 0
}
```

## Frontend Impact

The frontend team must make the following adjustments:

* **Remove** the existing AI Usage card that was mapped to `handbook_uploaded`.
* **Add** a new "Employees on Leave Today" card.
* **Read** the value from the newly added `employees_on_leave_today` field.

*Note: The Documents card mapping does not require any changes as long as it already uses `generated_documents_count`.*

## Files Changed

* `WakeelAI/Wakeel.Application/DTOs/Dashboard/DashboardSummaryResponse.cs`: Updated DTO to replace the AI Usage property with the new HR metric.
* `WakeelAI/Wakeel.Application/Services/DashboardService.cs`: Implemented the logic for `EmployeesOnLeaveToday` and real document counting.
* `WakeelAI/Wakeel.Application/Interfaces/Repositories/IGenericRepository.cs`: Added `CountAsync` method contract.
* `WakeelAI/Wakeel.Infrastructure/Repositories/GenericRepository.cs`: Implemented `CountAsync` method.
* `WakeelAI/Wakeel.Tests.Integration/Dashboard/DashboardEndpointTests.cs`: Updated integration tests for accurate counting, company isolation, and overlapping records.

## Validation

The implementation was tested successfully locally. No unrelated features were modified, and the `Develop` branch schema remains intact.
