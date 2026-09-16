using FCanteen.Data.Entities;
using System.Diagnostics;

namespace FCanteen.KitchenServer
{
    /// <summary>
    /// YC2: Tính "chỉ số hiệu quả món ăn" bằng 3 cách để so sánh tốc độ.
    /// Thuật toán cố tình dùng phép tính nặng (Sqrt, Pow, nối chuỗi) để CPU phải làm việc thật sự.
    /// </summary>
    public class PerformanceTestService
    {
        // Tính điểm hiệu quả cho 1 món: duyệt hết TicketLine rồi tính tỉ suất
        private double CalculateDishEfficiency(MenuItem item, List<TicketLine> allLines)
        {
            double score = 0;
            var itemLines = allLines.Where(l => l.MenuItemId == item.Id).ToList();

            foreach (var line in itemLines)
            {
                // Phép tính nặng giả lập → giúp thấy rõ khác biệt giữa đơn luồng và đa luồng
                double rawValue = (double)(line.UnitPrice * line.Quantity);
                score += Math.Sqrt(rawValue) * Math.Pow(line.Quantity, 1.5);

                string dummyStr = line.MenuItemId + "_" + line.OrderTicketId;
                if (dummyStr.Length > 100) score += 1;
            }
            return score;
        }

        // Cách 1: foreach tuần tự (1 luồng duy nhất)
        public long RunSequential(List<MenuItem> menuItems, List<TicketLine> allLines)
        {
            var sw = Stopwatch.StartNew();
            foreach (var item in menuItems)
                CalculateDishEfficiency(item, allLines);
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        // Cách 2: Parallel.ForEach (TPL tự chia luồng)
        public long RunParallelForEach(List<MenuItem> menuItems, List<TicketLine> allLines)
        {
            var sw = Stopwatch.StartNew();
            Parallel.ForEach(menuItems, item => CalculateDishEfficiency(item, allLines));
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        // Cách 3: Parallel.For (giới hạn số Core qua MaxDegreeOfParallelism)
        public long RunParallelFor(List<MenuItem> menuItems, List<TicketLine> allLines, int maxDegree)
        {
            var sw = Stopwatch.StartNew();
            Parallel.For(0, menuItems.Count,
                new ParallelOptions { MaxDegreeOfParallelism = maxDegree },
                i => CalculateDishEfficiency(menuItems[i], allLines));
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }
    }
}
