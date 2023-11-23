namespace SnapshotChat
{
    public class SnapshotState
    {
        public SnapshotStatus Status { get; set; }
        public string InitiatorProcess { get; set; }
        public List<string> Values { get; set; }
    }

    public enum SnapshotStatus
    {
        InProgress,
        Done
    }
}