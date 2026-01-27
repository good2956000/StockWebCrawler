using System;

namespace MvcWebCrawler.Models
{
    /// <summary>
    /// 個股交易量資料模型 (FinMind API)
    /// </summary>
    public class StockVolume
    {
        /// <summary>
        /// 日期
        /// </summary>
        public string date { get; set; }

        /// <summary>
        /// 股票代號
        /// </summary>
        public string stock_id { get; set; }

        /// <summary>
        /// 成交股數
        /// </summary>
        public string Trading_Volume { get; set; }

        /// <summary>
        /// 成交金額
        /// </summary>
        public string Trading_money { get; set; }

        /// <summary>
        /// 開盤價
        /// </summary>
        public string open { get; set; }

        /// <summary>
        /// 最高價
        /// </summary>
        public string max { get; set; }

        /// <summary>
        /// 最低價
        /// </summary>
        public string min { get; set; }

        /// <summary>
        /// 收盤價
        /// </summary>
        public string close { get; set; }

        /// <summary>
        /// 漲跌價差
        /// </summary>
        public string spread { get; set; }

        /// <summary>
        /// 成交筆數
        /// </summary>
        public string Trading_turnover { get; set; }

        // 以下是計算屬性，用於顯示
        
        /// <summary>
        /// 股票名稱（需要額外查詢或維護對照表）
        /// </summary>
        public string StockName { get; set; }

        /// <summary>
        /// 漲跌符號
        /// </summary>
        public string ChangeDirection
        {
            get
            {
                if (string.IsNullOrEmpty(spread)) return "";
                
                if (decimal.TryParse(spread, out decimal value))
                {
                    if (value > 0) return "▲";
                    else if (value < 0) return "▼";
                    else return "-";
                }
                return "";
            }
        }
    }
}
