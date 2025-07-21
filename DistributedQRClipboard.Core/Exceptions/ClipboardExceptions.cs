namespace DistributedQRClipboard.Core.Exceptions;

/// <summary>
/// Base exception class for all clipboard-related exceptions.
/// </summary>
public abstract class ClipboardException : Exception
{
    /// <summary>
    /// Gets the error code associated with this exception.
    /// </summary>
    public abstract string ErrorCode { get; }

    /// <summary>
    /// Gets additional context information about the error.
    /// </summary>
    public Dictionary<string, object> Context { get; } = new();

    /// <summary>
    /// Initializes a new instance of the ClipboardException class.
    /// </summary>
    protected ClipboardException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the ClipboardException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    protected ClipboardException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the ClipboardException class with a specified error message 
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    protected ClipboardException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Adds context information to the exception.
    /// </summary>
    /// <param name="key">The context key.</param>
    /// <param name="value">The context value.</param>
    /// <returns>This exception instance for method chaining.</returns>
    public ClipboardException WithContext(string key, object value)
    {
        Context[key] = value;
        return this;
    }
}

/// <summary>
/// Exception thrown when a requested session is not found.
/// </summary>
public sealed class SessionNotFoundException : ClipboardException
{
    /// <inheritdoc />
    public override string ErrorCode => "SESSION_NOT_FOUND";

    /// <summary>
    /// Gets the session ID that was not found.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Initializes a new instance of the SessionNotFoundException class.
    /// </summary>
    /// <param name="sessionId">The session ID that was not found.</param>
    public SessionNotFoundException(Guid sessionId)
        : base($"Session with ID '{sessionId}' was not found.")
    {
        SessionId = sessionId;
        WithContext("SessionId", sessionId);
    }

    /// <summary>
    /// Initializes a new instance of the SessionNotFoundException class with a custom message.
    /// </summary>
    /// <param name="sessionId">The session ID that was not found.</param>
    /// <param name="message">Custom error message.</param>
    public SessionNotFoundException(Guid sessionId, string message)
        : base(message)
    {
        SessionId = sessionId;
        WithContext("SessionId", sessionId);
    }

    /// <summary>
    /// Initializes a new instance of the SessionNotFoundException class with a custom message and inner exception.
    /// </summary>
    /// <param name="sessionId">The session ID that was not found.</param>
    /// <param name="message">Custom error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public SessionNotFoundException(Guid sessionId, string message, Exception innerException)
        : base(message, innerException)
    {
        SessionId = sessionId;
        WithContext("SessionId", sessionId);
    }
}

/// <summary>
/// Exception thrown when a session is in an invalid state for the requested operation.
/// </summary>
public sealed class InvalidSessionException : ClipboardException
{
    /// <inheritdoc />
    public override string ErrorCode => "INVALID_SESSION";

    /// <summary>
    /// Gets the session ID that is invalid.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Gets the reason why the session is invalid.
    /// </summary>
    public SessionInvalidReason Reason { get; }

    /// <summary>
    /// Initializes a new instance of the InvalidSessionException class.
    /// </summary>
    /// <param name="sessionId">The session ID that is invalid.</param>
    /// <param name="reason">The reason why the session is invalid.</param>
    public InvalidSessionException(Guid sessionId, SessionInvalidReason reason)
        : base($"Session '{sessionId}' is invalid: {reason}")
    {
        SessionId = sessionId;
        Reason = reason;
        WithContext("SessionId", sessionId)
            .WithContext("Reason", reason);
    }

    /// <summary>
    /// Initializes a new instance of the InvalidSessionException class with a custom message.
    /// </summary>
    /// <param name="sessionId">The session ID that is invalid.</param>
    /// <param name="reason">The reason why the session is invalid.</param>
    /// <param name="message">Custom error message.</param>
    public InvalidSessionException(Guid sessionId, SessionInvalidReason reason, string message)
        : base(message)
    {
        SessionId = sessionId;
        Reason = reason;
        WithContext("SessionId", sessionId)
            .WithContext("Reason", reason);
    }

    /// <summary>
    /// Initializes a new instance of the InvalidSessionException class with a custom message and inner exception.
    /// </summary>
    /// <param name="sessionId">The session ID that is invalid.</param>
    /// <param name="reason">The reason why the session is invalid.</param>
    /// <param name="message">Custom error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public InvalidSessionException(Guid sessionId, SessionInvalidReason reason, string message, Exception innerException)
        : base(message, innerException)
    {
        SessionId = sessionId;
        Reason = reason;
        WithContext("SessionId", sessionId)
            .WithContext("Reason", reason);
    }
}

/// <summary>
/// Exception thrown when clipboard content validation fails.
/// </summary>
public sealed class ClipboardValidationException : ClipboardException
{
    /// <inheritdoc />
    public override string ErrorCode => "CLIPBOARD_VALIDATION_FAILED";

    /// <summary>
    /// Gets the validation errors that occurred.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; }

    /// <summary>
    /// Gets the content that failed validation (truncated for security).
    /// </summary>
    public string? ContentPreview { get; }

    /// <summary>
    /// Initializes a new instance of the ClipboardValidationException class.
    /// </summary>
    /// <param name="validationErrors">The validation errors that occurred.</param>
    public ClipboardValidationException(IEnumerable<string> validationErrors)
        : base("Clipboard content validation failed.")
    {
        ValidationErrors = validationErrors.ToList().AsReadOnly();
        WithContext("ValidationErrors", ValidationErrors);
    }

    /// <summary>
    /// Initializes a new instance of the ClipboardValidationException class.
    /// </summary>
    /// <param name="validationErrors">The validation errors that occurred.</param>
    /// <param name="contentPreview">Preview of the content that failed validation.</param>
    public ClipboardValidationException(IEnumerable<string> validationErrors, string? contentPreview)
        : base("Clipboard content validation failed.")
    {
        ValidationErrors = validationErrors.ToList().AsReadOnly();
        ContentPreview = contentPreview?.Length > 100 ? contentPreview[..100] + "..." : contentPreview;
        WithContext("ValidationErrors", ValidationErrors);
        
        if (ContentPreview is not null)
        {
            WithContext("ContentPreview", ContentPreview);
        }
    }

    /// <summary>
    /// Initializes a new instance of the ClipboardValidationException class with a single validation error.
    /// </summary>
    /// <param name="validationError">The validation error that occurred.</param>
    public ClipboardValidationException(string validationError)
        : this(new[] { validationError })
    {
    }

    /// <summary>
    /// Initializes a new instance of the ClipboardValidationException class with a single validation error and content preview.
    /// </summary>
    /// <param name="validationError">The validation error that occurred.</param>
    /// <param name="contentPreview">Preview of the content that failed validation.</param>
    public ClipboardValidationException(string validationError, string? contentPreview)
        : this(new[] { validationError }, contentPreview)
    {
    }

    /// <summary>
    /// Initializes a new instance of the ClipboardValidationException class with custom message and inner exception.
    /// </summary>
    /// <param name="validationErrors">The validation errors that occurred.</param>
    /// <param name="message">Custom error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ClipboardValidationException(IEnumerable<string> validationErrors, string message, Exception innerException)
        : base(message, innerException)
    {
        ValidationErrors = validationErrors.ToList().AsReadOnly();
        WithContext("ValidationErrors", ValidationErrors);
    }
}

/// <summary>
/// Exception thrown when device operations fail.
/// </summary>
public sealed class DeviceOperationException : ClipboardException
{
    /// <inheritdoc />
    public override string ErrorCode => "DEVICE_OPERATION_FAILED";

    /// <summary>
    /// Gets the device ID that failed the operation.
    /// </summary>
    public Guid DeviceId { get; }

    /// <summary>
    /// Gets the operation that failed.
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Initializes a new instance of the DeviceOperationException class.
    /// </summary>
    /// <param name="deviceId">The device ID that failed the operation.</param>
    /// <param name="operation">The operation that failed.</param>
    public DeviceOperationException(Guid deviceId, string operation)
        : base($"Device operation '{operation}' failed for device '{deviceId}'.")
    {
        DeviceId = deviceId;
        Operation = operation;
        WithContext("DeviceId", deviceId)
            .WithContext("Operation", operation);
    }

    /// <summary>
    /// Initializes a new instance of the DeviceOperationException class with a custom message.
    /// </summary>
    /// <param name="deviceId">The device ID that failed the operation.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="message">Custom error message.</param>
    public DeviceOperationException(Guid deviceId, string operation, string message)
        : base(message)
    {
        DeviceId = deviceId;
        Operation = operation;
        WithContext("DeviceId", deviceId)
            .WithContext("Operation", operation);
    }

    /// <summary>
    /// Initializes a new instance of the DeviceOperationException class with a custom message and inner exception.
    /// </summary>
    /// <param name="deviceId">The device ID that failed the operation.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="message">Custom error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DeviceOperationException(Guid deviceId, string operation, string message, Exception innerException)
        : base(message, innerException)
    {
        DeviceId = deviceId;
        Operation = operation;
        WithContext("DeviceId", deviceId)
            .WithContext("Operation", operation);
    }
}

/// <summary>
/// Reasons why a session might be invalid.
/// </summary>
public enum SessionInvalidReason
{
    /// <summary>
    /// Session has expired.
    /// </summary>
    Expired,

    /// <summary>
    /// Session has reached maximum capacity.
    /// </summary>
    MaxCapacityReached,

    /// <summary>
    /// Session is in a closed state.
    /// </summary>
    Closed,

    /// <summary>
    /// Session has been terminated.
    /// </summary>
    Terminated,

    /// <summary>
    /// Session ID format is invalid.
    /// </summary>
    InvalidFormat,

    /// <summary>
    /// Device is not authorized to access this session.
    /// </summary>
    Unauthorized
}
