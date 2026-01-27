using System;

namespace MvcWebCrawler.Models
{
    public class MonthlyRevenue
    {
        /// <summary>
        /// 出表日期
        /// </summary>
        public string 出表日期 { get; set; }

        /// <summary>
        /// 資料年月
        /// </summary>
        public string 資料年月 { get; set; }

        /// <summary>
        /// 公司代號
        /// </summary>
        public string 公司代號 { get; set; }

        /// <summary>
        /// 公司名稱
        /// </summary>
        public string 公司名稱 { get; set; }

        /// <summary>
        /// 產業別
        /// </summary>
        public string 產業別 { get; set; }

        /// <summary>
        /// 營業收入-當月營收
        /// </summary>
        public string 當月營收 { get; set; }

        /// <summary>
        /// 營業收入-上月營收
        /// </summary>
        public string 上月營收 { get; set; }

        /// <summary>
        /// 營業收入-去年當月營收
        /// </summary>
        public string 去年當月營收 { get; set; }

        /// <summary>
        /// 營業收入-上月比較增減(%)
        /// </summary>
        public string 上月比較增減 { get; set; }

        /// <summary>
        /// 營業收入-去年同月增減(%)
        /// </summary>
        public string 去年同月增減 { get; set; }

        /// <summary>
        /// 累計營業收入-當月累計營收
        /// </summary>
        public string 當月累計營收 { get; set; }

        /// <summary>
        /// 累計營業收入-去年累計營收
        /// </summary>
        public string 去年累計營收 { get; set; }

        /// <summary>
        /// 累計營業收入-前期比較增減(%)
        /// </summary>
        public string 前期比較增減 { get; set; }

        /// <summary>
        /// 備註
        /// </summary>
        public string 備註 { get; set; }
    }
}
