using System;
using System.Collections.Generic;

namespace TimesheetApp.Models.TimesheetModels;

public partial class EfmigrationsHistory
{
    public string MigrationId { get; set; } = null!;

    public string ProductVersion { get; set; } = null!;
}
