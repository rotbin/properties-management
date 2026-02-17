using System.ComponentModel.DataAnnotations;

namespace BuildingManagement.Core.Entities.Notifications;

public class SmsTemplate
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(5)]
    public string Language { get; set; } = "he";

    /// <summary>Message body with placeholders: {{FullName}}, {{BuildingName}}, {{Period}}, {{AmountDue}}, {{Outstanding}}, {{PayLink}}</summary>
    [Required, MaxLength(1000)]
    public string Body { get; set; } = string.Empty;

    /// <summary>Email subject line with placeholders (used when sending via email)</summary>
    [MaxLength(300)]
    public string? EmailSubject { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
