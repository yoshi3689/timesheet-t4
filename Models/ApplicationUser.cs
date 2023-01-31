using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public virtual ICollection<Timesheet> Timesheets { get; } = new List<Timesheet>();

    }
}