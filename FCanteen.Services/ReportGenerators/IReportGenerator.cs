using FCanteen.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCanteen.Services.ReportGenerators
{
    public interface IReportGenerator
    {
        void GenerateReport(OrderTicket ticket);
    }
}
