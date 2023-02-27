using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TimesheetApp.Data;

namespace TimesheetApp.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmployeeController(ApplicationDbContext context)
        {
            _context = context;
        }
    }


    public class UniqueEmployeeNum : ValidationAttribute
    {
        public string GetErrorMessage() =>
            $"Employee number must be unique";

        protected override ValidationResult? IsValid(
            object? value, ValidationContext validationContext)
        {
            int num = Convert.ToInt32(value);
            var _context = (ApplicationDbContext)validationContext.GetService(typeof(ApplicationDbContext))!;
            var user = _context.Users.Where(c => c.EmployeeNumber == num);
            if (user.Count() == 0)
            {
                return ValidationResult.Success;
            }
            else
            {
                return new ValidationResult(GetErrorMessage());
            }
        }
    }


}