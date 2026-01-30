using System;

namespace MvcWebCrawler.Helpers
{
    /// <summary>
    /// 季度日期計算輔助類別
    /// </summary>
    public static class QuarterHelper
    {
        /// <summary>
        /// 取得最近可用的財報季度結束日期（根據當前時間往回推算）
        /// 財報通常在季度結束後 45 天公布
        /// </summary>
        public static DateTime GetLatestAvailableQuarterEndDate()
        {
            DateTime now = DateTime.Now;
            DateTime latestQuarter;

            // 財報延遲時間：季度結束後約 45 天公布
            int reportDelayDays = 45;

            // 判斷當前日期應該使用哪一季的財報
            if (now >= new DateTime(now.Year, 11, 15)) // 11/15 之後，Q3 財報應該已公布
            {
                latestQuarter = new DateTime(now.Year, 9, 30); // Q3
            }
            else if (now >= new DateTime(now.Year, 8, 15)) // 8/15 之後，Q2 財報應該已公布
            {
                latestQuarter = new DateTime(now.Year, 6, 30); // Q2
            }
            else if (now >= new DateTime(now.Year, 5, 15)) // 5/15 之後，Q1 財報應該已公布
            {
                latestQuarter = new DateTime(now.Year, 3, 31); // Q1
            }
            else if (now >= new DateTime(now.Year, 3, 15)) // 3/15 之後，上一年 Q4 財報應該已公布
            {
                latestQuarter = new DateTime(now.Year - 1, 12, 31); // 上一年 Q4
            }
            else
            {
                // 其他時間使用上一年 Q3
                latestQuarter = new DateTime(now.Year - 1, 9, 30);
            }

            return latestQuarter;
        }

        /// <summary>
        /// 取得上一季的結束日期
        /// </summary>
        public static DateTime GetPreviousQuarterEndDate(DateTime currentQuarterEndDate)
        {
            // 根據當前季度結束日期，計算上一季
            if (currentQuarterEndDate.Month == 12 && currentQuarterEndDate.Day == 31)
            {
                // Q4 -> Q3
                return new DateTime(currentQuarterEndDate.Year, 9, 30);
            }
            else if (currentQuarterEndDate.Month == 9 && currentQuarterEndDate.Day == 30)
            {
                // Q3 -> Q2
                return new DateTime(currentQuarterEndDate.Year, 6, 30);
            }
            else if (currentQuarterEndDate.Month == 6 && currentQuarterEndDate.Day == 30)
            {
                // Q2 -> Q1
                return new DateTime(currentQuarterEndDate.Year, 3, 31);
            }
            else if (currentQuarterEndDate.Month == 3 && currentQuarterEndDate.Day == 31)
            {
                // Q1 -> 上一年 Q4
                return new DateTime(currentQuarterEndDate.Year - 1, 12, 31);
            }
            else
            {
                throw new ArgumentException("Invalid quarter end date");
            }
        }

        /// <summary>
        /// 取得往回推算的 4 個季度結束日期
        /// </summary>
        public static DateTime[] GetLast4QuartersEndDates()
        {
            var quarters = new DateTime[4];
            var latestQuarter = GetLatestAvailableQuarterEndDate();
            
            quarters[0] = latestQuarter; // 最新季度
            quarters[1] = GetPreviousQuarterEndDate(quarters[0]);
            quarters[2] = GetPreviousQuarterEndDate(quarters[1]);
            quarters[3] = GetPreviousQuarterEndDate(quarters[2]);

            return quarters;
        }

        /// <summary>
        /// 將日期轉換為 API 格式字串 (yyyy-MM-dd)
        /// </summary>
        public static string ToApiDateString(DateTime date)
        {
            return date.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// 取得季度標籤 (例如: "2025 Q3")
        /// </summary>
        public static string GetQuarterLabel(DateTime quarterEndDate)
        {
            int quarter = GetQuarterNumber(quarterEndDate);
            return $"{quarterEndDate.Year} Q{quarter}";
        }

        /// <summary>
        /// 取得季度編號 (1-4)
        /// </summary>
        public static int GetQuarterNumber(DateTime date)
        {
            if (date.Month == 3 && date.Day == 31)
                return 1; // Q1
            else if (date.Month == 6 && date.Day == 30)
                return 2; // Q2
            else if (date.Month == 9 && date.Day == 30)
                return 3; // Q3
            else if (date.Month == 12 && date.Day == 31)
                return 4; // Q4
            else
                throw new ArgumentException("Invalid quarter end date");
        }

        /// <summary>
        /// 取得當季和上一季的日期字串 (用於 ROE 計算)
        /// </summary>
        public static (string currentQuarter, string previousQuarter) GetCurrentAndPreviousQuarterDates()
        {
            var latestQuarter = GetLatestAvailableQuarterEndDate();
            var previousQuarter = GetPreviousQuarterEndDate(latestQuarter);

            return (ToApiDateString(latestQuarter), ToApiDateString(previousQuarter));
        }
    }
}
