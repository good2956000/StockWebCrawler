namespace MvcWebCrawler.Models
{
    public class StockArticle
    {
        public string CompanyId { get; set; }      // 公司代號
        public string CompanyName { get; set; }    // 公司名稱
        public string Date { get; set; }           // 公告日期
        public string Time { get; set; }           // 公告時間
        public string Title { get; set; }          // 公告標題
        public string Url { get; set; }            // 詳細資料連結
    }
}
