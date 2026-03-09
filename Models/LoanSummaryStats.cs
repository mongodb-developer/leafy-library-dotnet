namespace Leafy_Library.Models;

public class LoanSummaryStats
{
    public int ActiveLoans { get; set; }
    public int Overdue { get; set; }
    public int DueSoon { get; set; }
    public int Returned { get; set; }
    public double? AvgReadingDays { get; set; }
}
