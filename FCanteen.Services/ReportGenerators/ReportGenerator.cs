using FCanteen.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCanteen.Services.ReportGenerators
{
    public class PdfReportGenerator : IReportGenerator
    {
        public void GenerateReport(OrderTicket ticket)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[METHOD INJECTION - XUAT BAO CAO PDF] Don hang: {ticket.TicketId} | Tong tien: {ticket.TotalAmount}d");
            Console.ResetColor();
        }
    }
}
