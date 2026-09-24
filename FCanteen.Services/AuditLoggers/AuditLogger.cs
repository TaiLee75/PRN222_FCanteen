using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCanteen.Services.AuditLoggers
{
    public class ConsoleAuditLogger : IAuditLogger
    {
        public void Log(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[AUDIT LOG] {DateTime.Now:HH:mm:ss} - {message}");
            Console.ResetColor();
        }
    }

}
