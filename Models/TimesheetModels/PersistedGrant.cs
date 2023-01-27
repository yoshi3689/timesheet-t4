using System;
using System.Collections.Generic;

namespace TimesheetApp.Models.TimesheetModels;

public partial class PersistedGrant
{
    public string Key { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string? SubjectId { get; set; }

    public string? SessionId { get; set; }

    public string ClientId { get; set; } = null!;

    public string? Description { get; set; }

    public string CreationTime { get; set; } = null!;

    public string? Expiration { get; set; }

    public string? ConsumedTime { get; set; }

    public string Data { get; set; } = null!;
}
