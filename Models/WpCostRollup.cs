namespace TimesheetApp.Models
{
    public class WpCostRollup
    {
        public double PmPlannedPD { get; set; }
        public double PmPlannedCost { get; set; }
        public double REPlannedPD { get; set; }
        public double REPlannedCost { get; set; }
        public double ActualPD { get; set; }
        public double ActualCost { get; set; }
        public double EacPD { get; set; }
        public double EacCost { get; set; }
        public double PdVariance { get; set; }
        public double CostVariance { get; set; }
        public double PercentComplete { get; set; }
    }
}
