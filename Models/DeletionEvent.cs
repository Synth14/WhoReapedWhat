namespace WhoReapedWhat.Models
{
    public class DeletionEvent
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public DateTime DeletedAt { get; set; }
    }
}
