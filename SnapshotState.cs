namespace SnapshotChat
{
    public class SnapshotState
    {
        public SnapshotStatus Status { get; set; }
        public string InitiatorProcess { get; set; }
    }

    public enum SnapshotStatus
    {
        InProgress,
        Done
    }
}