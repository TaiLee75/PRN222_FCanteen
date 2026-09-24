using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCanteen.Services.AuditLoggers
{
   
    // Phụ thuộc không bắt buộc (Optional)
    public interface IAuditLogger
    {
        void Log(string message);
    }

}
