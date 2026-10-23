namespace PublicServiceCaseManagement.Api.Domain.Notifications;

public static class NotificationTypes
{
    public const string CaseSubmitted = "CaseSubmitted";
    public const string CaseAssigned = "CaseAssigned";
    public const string CaseStatusChanged = "CaseStatusChanged";
}

public static class NotificationDeliveryStatuses
{
    public const string Queued = "Queued";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
}

public sealed class Notification
{
    private Notification()
    {
    }

    public Guid Id { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? RelatedEntityType { get; private set; }
    public Guid? RelatedEntityId { get; private set; }
    public bool IsRead { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public string DeliveryChannel { get; private set; } = "InApp,Email";
    public string DeliveryStatus { get; private set; } = NotificationDeliveryStatuses.Queued;
    public Guid? BackgroundJobId { get; private set; }
    public string? DeliveryErrorCode { get; private set; }
    public string? DeliveryErrorDetails { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;

    public static Notification Create(
        Guid recipientUserId,
        string type,
        string title,
        string message,
        string? relatedEntityType,
        Guid? relatedEntityId,
        string idempotencyKey,
        DateTimeOffset now,
        DateTimeOffset? expiresAt = null)
    {
        if (recipientUserId == Guid.Empty)
            throw new ArgumentException("A recipient is required.", nameof(recipientUserId));
        if (string.IsNullOrWhiteSpace(type) || type.Length > 50)
            throw new ArgumentException("A valid notification type is required.", nameof(type));
        if (string.IsNullOrWhiteSpace(title) || title.Length > 150)
            throw new ArgumentException("A valid notification title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(message) || message.Length > 500)
            throw new ArgumentException("A valid notification message is required.", nameof(message));
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
            throw new ArgumentException("A valid idempotency key is required.", nameof(idempotencyKey));

        return new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            Type = type,
            Title = title,
            Message = message,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            IdempotencyKey = idempotencyKey
        };
    }

    public void AssociateJob(Guid jobId) => BackgroundJobId = jobId;

    public void MarkRead(DateTimeOffset now)
    {
        if (IsRead)
            return;
        IsRead = true;
        ReadAt = now;
    }

    public void MarkEmailSent()
    {
        DeliveryStatus = NotificationDeliveryStatuses.Sent;
        DeliveryErrorCode = null;
        DeliveryErrorDetails = null;
    }

    public void MarkEmailFailed(string errorCode, string errorDetails)
    {
        DeliveryStatus = NotificationDeliveryStatuses.Failed;
        DeliveryErrorCode = errorCode;
        DeliveryErrorDetails = errorDetails;
    }
}
