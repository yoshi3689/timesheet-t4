using System;
using System.Collections.Generic;

namespace TimesheetApp.Models.TimesheetModels;

public partial class Key
{
    public string Id { get; set; } = null!;

    public int Version { get; set; }

    public string Created { get; set; } = null!;

    public string? Use { get; set; }

    public string Algorithm { get; set; } = null!;

    public int IsX509certificate { get; set; }

    public int DataProtected { get; set; }

    public string Data { get; set; } = null!;
}
