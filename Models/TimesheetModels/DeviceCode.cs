using System;
using System.Collections.Generic;

namespace TimesheetApp.Models.TimesheetModels;

public partial class DeviceCode
{
    public string UserCode { get; set; } = null!;

    public string DeviceCode1 { get; set; } = null!;

    public string? SubjectId { get; set; }

    public string? SessionId { get; set; }

    public string ClientId { get; set; } = null!;

    public string? Description { get; set; }

    public string CreationTime { get; set; } = null!;

    public string Expiration { get; set; } = null!;

    public string Data { get; set; } = null!;
}
