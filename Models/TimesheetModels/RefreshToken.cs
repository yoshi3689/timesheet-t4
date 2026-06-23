using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimesheetApp.Models.TimesheetModels;

public class RefreshToken
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = null!;

    [Required]
    [MaxLength(64)]
    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;

    [ForeignKey("UserId")]
    public TimesheetApp.Models.ApplicationUser? User { get; set; }
}
