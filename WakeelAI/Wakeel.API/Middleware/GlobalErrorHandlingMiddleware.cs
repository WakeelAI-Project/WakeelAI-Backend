using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Wakeel.API.Middleware;

/// <summary>
/// Catches all unhandled exceptions and converts them to the standard API v2 error envelope
/// { error, message, status } using the project's status semantics.
/// </summary>
public class GlobalErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalErrorHandlingMiddleware> _logger;

    public GlobalErrorHandlingMiddleware(RequestDelegate next, ILogger<GlobalErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing request {Method} {Path}", context.Request.Method, context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        (int status, string error, string message) = MapException(ex);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = status;

        var payload = new
        {
            error,
            message,
            status
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
        var json = JsonSerializer.Serialize(payload, options);
        return context.Response.WriteAsync(json);
    }

    private static (int status, string error, string message) MapException(Exception ex)
    {
        // Domain-style InvalidOperationException carrying a machine-readable code in Message
        if (ex is InvalidOperationException ioe)
        {
            return ioe.Message switch
            {
                "validation_error" => (StatusCodes.Status400BadRequest, "validation_error", "Validation failed."),
                "email_already_exists" => (StatusCodes.Status409Conflict, "email_already_exists", "Email already exists."),
                "department_not_found" => (StatusCodes.Status404NotFound, "department_not_found", "Department not found."),
                "employee_not_found" => (StatusCodes.Status404NotFound, "employee_not_found", "Employee not found."),
                "leave_request_not_found" => (StatusCodes.Status404NotFound, "leave_request_not_found", "Leave request not found."),
                "document_not_found" => (StatusCodes.Status404NotFound, "document_not_found", "Document not found."),
                "template_not_found" => (StatusCodes.Status404NotFound, "template_not_found", "Template not found."),
                "insufficient_leave_balance" => (StatusCodes.Status422UnprocessableEntity, "insufficient_leave_balance", "Insufficient leave balance."),
                "attachment_required" => (StatusCodes.Status422UnprocessableEntity, "attachment_required", "Attachment is required."),
                "not_a_draft" => (StatusCodes.Status409Conflict, "not_a_draft", "Operation not allowed in current state."),
                "not_pending" => (StatusCodes.Status409Conflict, "not_pending", "Operation not allowed in current state."),
                "not_finalized" => (StatusCodes.Status409Conflict, "not_finalized", "Operation not allowed in current state."),
                "employee_not_assigned" => (StatusCodes.Status400BadRequest, "employee_not_assigned", "Employee is not assigned."),
                _ => (StatusCodes.Status500InternalServerError, "internal_error", ex.Message ?? "An unexpected error occurred.")
            };
        }

        if (ex is UnauthorizedAccessException)
            return (StatusCodes.Status403Forbidden, "forbidden", "Access denied.");

        if (ex is ArgumentException)
            return (StatusCodes.Status400BadRequest, "validation_error", ex.Message);

        // Fallback
        return (StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred.");
    }
}
