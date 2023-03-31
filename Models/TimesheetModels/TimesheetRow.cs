using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimesheetApp.Models.TimesheetModels;

/// <summary>
/// Model for a specific row in a timesheet.
/// For the database.
/// </summary>
public partial class TimesheetRow
{

    [Key]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TimesheetRowId { get; set; }

    public int? ProjectId
    {
        get
        {
            return WorkPackageProjectId;
        }
        set
        {
            WorkPackageProjectId = value;
        }
    }

    public double TotalHoursRow { get; set; }
    [Required]
    [MaxLength(255)]
    public string? WorkPackageId { get; set; }

    [Required]
    public int? WorkPackageProjectId { get; set; }

    public string? Notes { get; set; }
    [NotMapped]
    public float Sat
    {
        get
        {
            return getHour(SAT);
        }
        set
        {
            setHour(SAT, value);
            TotalHoursRow = getSum();
        }
    }
    [NotMapped]
    public float Sun
    {
        get
        {
            return getHour(SUN);
        }
        set
        {
            setHour(SUN, value);
            TotalHoursRow = getSum();
        }
    }
    [NotMapped]
    public float Mon
    {
        get
        {
            return getHour(MON);
        }
        set
        {
            setHour(MON, value);
            TotalHoursRow = getSum();
        }
    }
    [NotMapped]
    public float Tue
    {
        get
        {
            return getHour(TUE);
        }
        set
        {
            setHour(TUE, value);
            TotalHoursRow = getSum();
        }
    }
    [NotMapped]
    public float Wed
    {
        get
        {
            return getHour(WED);
        }
        set
        {
            setHour(WED, value);
            TotalHoursRow = getSum();
        }
    }
    [NotMapped]
    public float Thu
    {
        get
        {
            return getHour(THU);
        }
        set
        {
            setHour(THU, value);
            TotalHoursRow = getSum();
        }
    }
    [NotMapped]
    public float Fri
    {
        get
        {
            return getHour(FRI);
        }
        set
        {
            setHour(FRI, value);
            TotalHoursRow = getSum();
        }
    }

    public long packedHours { get; set; }
    public string? OriginalLabourCode { get; set; }
    [Required]
    public int TimesheetId { get; set; }
    [ForeignKey("TimesheetId")]
    public Timesheet? Timesheet { get; set; }
    [ForeignKey("ProjectId")]
    public Project? Project { get; set; }
    //set dbcontext for fk
    public WorkPackage? WorkPackage { get; set; }

    [NotMapped]
    public Dictionary<int, string>? ValidationErrors { get; set; }

    //system converted over from bruces timesheet, so columns don't take as much room in the db.
    public const int SAT = 0;
    public const int SUN = 1;
    public const int MON = 2;
    public const int TUE = 3;
    public const int WED = 4;
    public const int THU = 5;
    public const int FRI = 6;
    public const float BASE10 = 10.0F;
    public const int FIRST_DAY = TimesheetRow.SAT;
    public const int LAST_DAY = TimesheetRow.FRI;
    public const int DAYS_IN_WEEK = 7;
    public const double HOURS_IN_DAY = 24.0;
    public const int DECIHOURS_IN_DAY = 240;
    public const double FULL_WORK_WEEK_HOURS = 40.0;
    public const int FULL_WORK_WEEK_DECIHOURS = 400;
    private const long serialVersionUID = 4L;
    private readonly long[] MASK = { 255L, 65280L, 16711680L, 4278190080L, 1095216660480L, 280375465082880L, 71776119061217280L };
    private readonly long[] UMASK = { -256L, -65281L, -16711681L, -4278190081L, -1095216660481L, -280375465082881L, -71776119061217281L };
    private const int BYTE_BASE = 256;
    private const int DECI_MAX = 240;
    private const int BITS_PER_BYTE = 8;
    public static int toDecihour(float hour)
    {
        return (int)Math.Round(hour * TimesheetRow.BASE10);
    }
    public static float toHour(int decihour)
    {
        float result = decihour / TimesheetRow.BASE10;
        float main = (float)Math.Floor(result);
        float secondPart = (float)Math.Round(((result - (int)result) * 25) / 10, 2);
        return main + secondPart;
    }
    public float getHour(int d)
    {
        return toHour(getDecihour(d));
    }
    public void setHour(int d, float charge)
    {
        if (charge < 0.0 || charge > HOURS_IN_DAY)
        {
            ValidationErrors = (ValidationErrors == null) ? new Dictionary<int, string>() : ValidationErrors;
            ValidationErrors.Add(d, "Hours in a cell must be between 0 and 24");
            return;
        }
        charge = (float)Math.Floor(charge) + ((float)Math.Round((charge - (int)charge) / 0.25f) / 10);

        setDecihour(d, toDecihour(charge));
    }
    public float getSum()
    {
        return TimesheetRow.toHour(this.getDeciSum());
    }
    public int getDeciSum()
    {
        int[] charges = this.getDecihours();
        int sum = 0;
        foreach (int charge in charges)
        {
            sum += charge;
        }
        return sum;
    }
    public int getDecihour(int d)
    {
        if (d < TimesheetRow.FIRST_DAY || d > TimesheetRow.LAST_DAY)
        {
            throw new Exception("day number out of range");
        }
        return (int)((this.packedHours & this.MASK[d]) >> d * TimesheetRow.BITS_PER_BYTE);
    }
    public void setDecihour(int d, int charge)
    {
        if (d < TimesheetRow.FIRST_DAY || d > TimesheetRow.LAST_DAY)
        {
            throw new Exception("day number out of range, must be in 0 .. 6");
        }
        if (charge < 0 || charge > TimesheetRow.DECI_MAX)
        {
            throw new Exception("charge out of range, must be 0 .. 240");
        }
        this.packedHours = this.packedHours & this.UMASK[d] | (long)charge << (d * TimesheetRow.BITS_PER_BYTE);
    }
    public float[] getHours()
    {
        float[] result = new float[LAST_DAY + 1];
        long check = this.packedHours;
        for (int i = TimesheetRow.FIRST_DAY; i <= TimesheetRow.LAST_DAY; i++)
        {
            result[i] = check % TimesheetRow.BYTE_BASE / TimesheetRow.BASE10;
            check /= TimesheetRow.BYTE_BASE;
        }
        return result;
    }
    public void setHours(float[] charges)
    {
        foreach (float charge in charges)
        {
            if (charge < 0.0 || charge > TimesheetRow.HOURS_IN_DAY)
            {
                throw new Exception("charge is out of maximum hours in day range");
            }
        }
        int result = 0;
        for (int i = TimesheetRow.LAST_DAY; i >= TimesheetRow.FIRST_DAY; i--)
        {
            result = result * TimesheetRow.BYTE_BASE + TimesheetRow.toDecihour(charges[i]);
        }
        this.packedHours = result;
    }
    public int[] getDecihours()
    {
        int[] result = new int[TimesheetRow.LAST_DAY + 1];
        long check = this.packedHours;
        for (int i = TimesheetRow.FIRST_DAY; i <= TimesheetRow.LAST_DAY; i++)
        {
            result[i] = (int)(check % TimesheetRow.BYTE_BASE);
            check /= TimesheetRow.BYTE_BASE;
        }
        return result;
    }
    public void setDecihours(int[] charges)
    {
        foreach (float charge in charges)
        {
            if (charge < 0 || charge > TimesheetRow.DECIHOURS_IN_DAY)
            {
                throw new Exception("charge is out of maximum hours in day range");
            }
        }
        int result = 0;
        for (int i = TimesheetRow.LAST_DAY; i >= TimesheetRow.FIRST_DAY; i--)
        {
            result = result * TimesheetRow.BYTE_BASE + charges[i];
        }
        this.packedHours = result;
    }
    private void checkHoursForWeek(long packedDecihours)
    {
        if (packedDecihours < 0)
        {
            throw new Exception("improperly formed packedHours < 0");
        }
        long check = packedDecihours;
        for (int i = TimesheetRow.FIRST_DAY; i <= TimesheetRow.LAST_DAY; i++)
        {
            if (check % TimesheetRow.BYTE_BASE > TimesheetRow.DECIHOURS_IN_DAY)
            {
                throw new Exception("improperly formed packedHours");
            }
            check /= TimesheetRow.BYTE_BASE;
        }
        if (check > 0)
        {
            throw new Exception("improperly formed packedHours");
        }
    }
}
