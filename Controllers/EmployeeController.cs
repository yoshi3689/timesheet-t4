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

    /// <summary>
    /// This class is used to verify that an employee has a unique employee num. It is used as an annotation in the ApplicationUser class.
    /// </summary>
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

    public class IntLengthAttribute : ValidationAttribute
    {
        public int MinLength { get; set; }
        public int MaxLength { get; set; }

        public IntLengthAttribute(int minLength, int maxLength)
        {
            MinLength = minLength;
            MaxLength = maxLength;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value != null)
            {
                int intValue = (int)value;
                int numDigits = intValue.ToString().Length;

                if (numDigits < MinLength || numDigits > MaxLength)
                {
                    return new ValidationResult($"Must be between {MinLength} and {MaxLength} digits long.");
                }
            }

            return ValidationResult.Success;
        }
    }


}