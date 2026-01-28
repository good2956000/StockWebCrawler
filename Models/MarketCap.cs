using System;

namespace MvcWebCrawler.Models
{
    /// <summary>
    /// 上市上櫃市值排行資料模型
    /// </summary>
    public class MarketCap
    {
        /// <summary>
        /// 排名
        /// </summary>
        public int Rank { get; set; }

        /// <summary>
        /// 股票代號
        /// </summary>
        public string StockId { get; set; }

        /// <summary>
        /// 股票名稱
        /// </summary>
        public string StockName { get; set; }

        /// <summary>
        /// 市值（億元）
        /// </summary>
        public string MarketValue { get; set; }

        /// <summary>
        /// 收盤價
        /// </summary>
        public string ClosePrice { get; set; }

        /// <summary>
        /// 漲跌
        /// </summary>
        public string Change { get; set; }

        /// <summary>
        /// 漲跌幅(%)
        /// </summary>
        public string ChangePercent { get; set; }

        /// <summary>
        /// 成交量（張）
        /// </summary>
        public string Volume { get; set; }

        /// <summary>
        /// 市場別（上市/上櫃）
        /// </summary>
        public string Market { get; set; }

        /// <summary>
        /// 產業別
        /// </summary>
        public string Industry { get; set; }

        /// <summary>
        /// 更新日期
        /// </summary>
        public string UpdateDate { get; set; }

        /// <summary>
        /// 漲跌方向符號
        /// </summary>
        public string ChangeDirection
        {
            get
            {
                if (string.IsNullOrEmpty(Change)) return "-";
                
                if (decimal.TryParse(Change, out decimal value))
                {
                    if (value > 0) return "▲";
                    else if (value < 0) return "▼";
                    else return "-";
                }
                return "-";
            }
        }
    }
}
