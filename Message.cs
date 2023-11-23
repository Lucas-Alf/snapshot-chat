namespace SnapshotChat
{
    public class Message
    {
        public string Marker { get; set; }
        public string Sender { get; set; }
        public List<string> Values { get; set; }
    }
}