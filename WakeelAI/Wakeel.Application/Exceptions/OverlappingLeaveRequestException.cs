using System;

namespace Wakeel.Application.Exceptions;

/// <summary>
/// Thrown when a leave request's dates overlap an existing Pending or Approved request.
/// Carries the conflicting request's type and dates so the API layer can build a message
/// that names them instead of a generic "you already have an overlapping request".
/// Message stays "overlapping_leave_request" - the existing error-code contract both
/// clients match on - so every existing message-based catch keeps working unchanged.
/// </summary>
public class OverlappingLeaveRequestException : InvalidOperationException
{
    public string ConflictingLeaveType { get; }
    public DateOnly ConflictingStartDate { get; }
    public DateOnly ConflictingEndDate { get; }
    public string ConflictingStatus { get; }

    public OverlappingLeaveRequestException(string conflictingLeaveType, DateOnly conflictingStartDate, DateOnly conflictingEndDate, string conflictingStatus)
        : base("overlapping_leave_request")
    {
        ConflictingLeaveType = conflictingLeaveType;
        ConflictingStartDate = conflictingStartDate;
        ConflictingEndDate = conflictingEndDate;
        ConflictingStatus = conflictingStatus;
    }
}
