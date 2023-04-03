using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Models
{
  public class WorkPackageNotificationModel
  {
    public string? Name { get; set; }
    public WorkPackage? WorkPackage { get; set; }
    public List<WorkPackage>? WorkPackages { get; set; }

    public List<Notification>? Notifications { get; set; }
  }
}