using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimesheetApp.Models.TimesheetModels;

// All 7 days of work hours are bit-packed into a single long (packedHours).
// Each day occupies 8 bits (one byte), ordered SAT→FRI in bits 0..55.
// Hours are stored as "decihours" (tenths), but with a custom quarter-hour
// encoding: 0.25h→1, 0.50h→2, 0.75h→3 (not 2, 5, 7). See toHour/setHour.
public partial class TimesheetRow
{

    [Key]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TimesheetRowId { get; set; }

    // Legacy alias for WorkPackageProjectId — kept for EF FK navigation attribute below.
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

    // Cached sum of all 7 days stored in DB. Updated automatically whenever a day
    // property setter (Sat–Fri) is called. Goes stale if setDecihour() is called directly.
    public double TotalHoursRow { get; set; }
    [Required]
    [MaxLength(255)]
    public string? WorkPackageId { get; set; }

    [Required]
    public int? WorkPackageProjectId { get; set; }

    public string? Notes { get; set; }

    // [NotMapped] day properties are not DB columns — they decode packedHours on read
    // and re-encode + refresh TotalHoursRow on write.
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

    // The only hours column stored in DB. Layout (low→high bytes): SAT SUN MON TUE WED THU FRI _
    // Each byte holds a decihour value 0..240 (= 0..24h in custom tenth encoding).
    [Range(0, long.MaxValue, ErrorMessage = "Only positive number allowed.")]
    public long packedHours { get; set; }
    public string? OriginalLabourCode { get; set; }
    [Required]
    public int TimesheetId { get; set; }
    [ForeignKey("TimesheetId")]
    public Timesheet? Timesheet { get; set; }
    [ForeignKey("ProjectId")]
    public Project? Project { get; set; }
    public WorkPackage? WorkPackage { get; set; }

    // Populated by setHour() when a value is out of range. Keyed by day constant (SAT..FRI).
    // Caller must check this after setting hours — no exception is thrown on validation failure.
    [NotMapped]
    public Dictionary<int, string>? ValidationErrors { get; set; }

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

    // MASK[d] isolates the 8 bits for day d: AND with packedHours → only that day's byte remains.
    // Values are 0xFF shifted left by d*8: MASK[0]=0xFF, MASK[1]=0xFF00, MASK[2]=0xFF0000, ...
    private static readonly long[] MASK = { 255L, 65280L, 16711680L, 4278190080L, 1095216660480L, 280375465082880L, 71776119061217280L };

    // UMASK[d] is the bitwise NOT of MASK[d] — AND with packedHours clears day d's byte,
    // leaving all other days untouched. Used in setDecihour before OR-ing in the new value.
    private static readonly long[] UMASK = { -256L, -65281L, -16711681L, -4278190081L, -1095216660481L, -280375465082881L, -71776119061217281L };

    // 2^8 = 256 possible values per byte slot, matching 8 bits per day.
    private const int BYTE_BASE = 256;
    // Maximum decihour value per day: 24 hours × 10 = 240.
    private const int DECI_MAX = 240;
    private const int BITS_PER_BYTE = 8;

    // Converts display hours to the stored decihour value.
    // Example: 7.5h → 75, 7.25h → 72.5 → rounds to 73 (quarter encoding, see toHour).
    public static int toDecihour(float hour)
    {
        return (int)Math.Round(hour * TimesheetRow.BASE10);
    }

    // Converts a stored decihour back to display hours.
    // The fractional part uses a custom quarter-hour encoding: stored tenths 1,2,3
    // map to 0.25, 0.50, 0.75 via `fraction * 25 / 10`. (Not a simple ÷10 — 7.1 stored ≠ 7.1h.)
    public static float toHour(int decihour)
    {
        float result = decihour / TimesheetRow.BASE10;
        float main = (float)Math.Floor(result);
        // e.g. result=7.3 → fraction=0.3 → 0.3*25/10=0.75 → display as 7.75h
        float secondPart = (float)Math.Round(((result - (int)result) * 25) / 10, 2);
        return main + secondPart;
    }

    public float getHour(int d)
    {
        return toHour(getDecihour(d));
    }

    // Validates range, then encodes the input into the custom quarter-hour format before packing.
    // On validation failure, adds to ValidationErrors (keyed by d) and returns — no exception.
    public void setHour(int d, float charge)
    {
        if (charge < 0.0 || charge > HOURS_IN_DAY)
        {
            ValidationErrors = (ValidationErrors == null) ? new Dictionary<int, string>() : ValidationErrors;
            ValidationErrors.Add(d, "Hours in a cell must be between 0 and 24");
            return;
        }
        // Round fractional part to nearest 0.25, then convert to custom tenth: 0.25→0.1, 0.50→0.2, 0.75→0.3
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

    // Extracts day d's decihour from packedHours using a bitmask + right-shift.
    // Equivalent to: (packedHours & MASK[d]) >> (d * 8)
    public int getDecihour(int d)
    {
        if (d < TimesheetRow.FIRST_DAY || d > TimesheetRow.LAST_DAY)
        {
            throw new Exception("day number out of range");
        }
        return (int)((this.packedHours & TimesheetRow.MASK[d]) >> d * TimesheetRow.BITS_PER_BYTE);
    }

    // Writes day d's decihour into packedHours without touching the other 6 days.
    // Step 1: clear day d's byte with UMASK (AND). Step 2: shift charge into position, OR it in.
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
        this.packedHours = this.packedHours & TimesheetRow.UMASK[d] | (long)charge << (d * TimesheetRow.BITS_PER_BYTE);
    }

    // Bulk extraction of all 7 days. Uses repeated modulo/divide instead of 7 mask operations:
    // each iteration takes the lowest byte (% 256) then shifts the whole value right by 8 bits (/ 256).
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
}
