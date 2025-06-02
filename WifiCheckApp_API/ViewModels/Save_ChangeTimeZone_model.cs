namespace WifiCheckApp_API.ViewModels
{
    public class Save_ChangeTimeZone_model
    {
        public int AttendanceId { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public string? Reason { get; set; }
        public int PerformedBy { get; set; }
    }
}
