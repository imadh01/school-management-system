namespace SchoolManagement.Domain.Entities;

/// <summary>
/// E2: the school's own record. Exactly one row (Id = 1, CHECK constraint): there is one school
/// (locked decision 1). Values the SCHOOL decides live here and are edited on the Settings page;
/// values for the people running the server (storage folder, token lifetimes...) stay in appsettings.
/// </summary>
public class SchoolSettings : IHasRowVersion
{
    /// <summary>The only row.</summary>
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public string SchoolName { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }

    /// <summary>The school's postal address as free text.</summary>
    public string? AddressText { get; set; }

    /// <summary>The school's country. Only PRE-SELECTS the country in address forms (decision 6); never assumed on save.</summary>
    public int? CountryId { get; set; }
    public Country? Country { get; set; }

    /// <summary>IANA id, e.g. "Asia/Dubai". Validated as a real time zone on save. F2 uses it for "today".</summary>
    public string TimeZoneId { get; set; } = string.Empty;

    /// <summary>ISO 4217, e.g. "AED", "INR".</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Bit mask of the days the school is open (WorkingDays.Monday = 1 ... Sunday = 64).</summary>
    public byte WorkingDays { get; set; }

    /// <summary>Text for the About page.</summary>
    public string? AboutTagline { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>The bits of <see cref="SchoolSettings.WorkingDays"/>.</summary>
public static class WorkingDays
{
    public const byte Monday = 1;
    public const byte Tuesday = 2;
    public const byte Wednesday = 4;
    public const byte Thursday = 8;
    public const byte Friday = 16;
    public const byte Saturday = 32;
    public const byte Sunday = 64;

    /// <summary>Every day set.</summary>
    public const byte All = 127;

    /// <summary>Monday first, as the Settings page shows them.</summary>
    public static readonly IReadOnlyList<(DayOfWeek Day, byte Bit)> Order = new[]
    {
        (DayOfWeek.Monday, Monday), (DayOfWeek.Tuesday, Tuesday), (DayOfWeek.Wednesday, Wednesday),
        (DayOfWeek.Thursday, Thursday), (DayOfWeek.Friday, Friday), (DayOfWeek.Saturday, Saturday),
        (DayOfWeek.Sunday, Sunday),
    };

    public static byte FromDays(IEnumerable<DayOfWeek> days) =>
        (byte)days.Distinct().Sum(d => Order.First(o => o.Day == d).Bit);

    public static List<DayOfWeek> ToDays(byte mask) =>
        Order.Where(o => (mask & o.Bit) != 0).Select(o => o.Day).ToList();
}
