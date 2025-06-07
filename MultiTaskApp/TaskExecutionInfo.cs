public class TaskExecutionInfo
{
    public int WorkerId { get; set; }
    public int TaskId { get; set; }
    public string ApiValue { get; set; } // Optional, if you want to store API response
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
}