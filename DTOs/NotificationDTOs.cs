namespace AlFolk.Api.DTOs;

public record SendNotificationRequest(
    string Title,
    string Message,
    string RecipientType, // "all" or "specific"
    Guid? TargetParentId
);

public record FcmTokenRequest(
    string FcmToken,
    string? DeviceType // "Android" / "IOS" / "Web"
);

public record TestFcmRequest(
    Guid ParentId,
    string FcmToken,
    string? Title,
    string? Message
);
