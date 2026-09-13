using System.ComponentModel.DataAnnotations;

namespace FCanteen.Data.Entities
{
    public class DeviceLog
    {
        [Key]
        public int Id { get; set; }
        public string Protocol { get; set; } = string.Empty; // TCP/UDP/HTTP/DNS
        public string SourceAddress { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}