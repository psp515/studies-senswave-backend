namespace Senswave.Presentation.Seed.Devices.Options;

public class PicoControllerOptions
{
    public bool Enabled { get; set; } = false;

    public string GlobalTopic { get; set; } = string.Empty;

    /// <summary>
    /// Additional operations of mixed types (number, boolean, options, hex color) added to the controller on top of its
    /// own four. The performance tests use 26, so the device holds 30 operations (the per-device limit).
    /// </summary>
    public int ExtraOperations { get; set; } = 0;

    /// <summary>Rows of an additional "Large" dashboard filled cell by cell with 1x1 widgets; 0 disables it (max 10).</summary>
    public int LargeDashboardRows { get; set; } = 0;

    /// <summary>Columns of the "Large" dashboard (max 5).</summary>
    public int LargeDashboardColumns { get; set; } = 4;
}
