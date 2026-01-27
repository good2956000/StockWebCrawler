using HtmlAgilityPack;
using MvcWebCrawler.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace MvcWebCrawler.Controllers
{
    public class StockController : Controller
    {
        public async Task<ActionResult> Index()
        {
            // 範例：只撈 6177、2330 兩家
            var companyIds = new List<string> { "6177", "2548", "3703", "5213", "2371", "6186", "2542", "5534", "3231", "3019", "2354", "2537", "5508", "2323", "1608" };
            var articles = await GetStockArticlesAsync(companyIds);
            return View(articles);
        }

        private async Task<List<StockArticle>> GetStockArticlesAsync(List<string> companyIds)
        {
            var result = new List<StockArticle>();
            string url = "https://mopsov.twse.com.tw/mops/web/t05sr01_1";

            using (HttpClient client = new HttpClient())
            {
                var html = await client.GetStringAsync(url);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                // 取得所有 tr
                var rows = htmlDoc.DocumentNode.SelectNodes("//tr[contains(@class, 'even') or contains(@class, 'odd')]");
                if (rows != null)
                {
                    foreach (var row in rows)
                    {
                        var cells = row.SelectNodes("td");
                        if (cells == null || cells.Count < 6) continue;

                        // 取得公司代號
                        string companyId = cells[0].InnerText.Trim();

                        // 判斷公司代號是否在參數清單中
                        if (!companyIds.Contains(companyId)) continue;

                        // 取得公司簡稱
                        string companyName = cells[1].InnerText.Trim();
                        // 取得標題
                        string title = cells[4].InnerText.Trim();

                        // 取得詳細資料 onclick 參數
                        var inputNode = cells[5].SelectSingleNode(".//input[@type='button']");
                        string onclick = inputNode?.GetAttributeValue("onclick", "");
                        string detailUrl = ParseDetailUrl(onclick);

                        result.Add(new StockArticle
                        {
                            CompanyId = companyId,
                            CompanyName = companyName,
                            Title = title,
                            Url = detailUrl
                        });
                    }
                }
            }

            return result;
        }
        // 解析 onclick 參數，組合詳細資料連結
        private string ParseDetailUrl(string onclick)
        {
            if (string.IsNullOrEmpty(onclick)) return "";

            var dict = new Dictionary<string, string>();
            var matches = System.Text.RegularExpressions.Regex.Matches(onclick, @"fm_t05sr01_1\.([A-Z_]+)\.value='([^']*)'");
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                dict[m.Groups[1].Value] = m.Groups[2].Value;
            }

            // 取值時用 TryGetValue
            string seqNo = dict.ContainsKey("SEQ_NO") ? dict["SEQ_NO"] : "";
            string spokeTime = dict.ContainsKey("SPOKE_TIME") ? dict["SPOKE_TIME"] : "";
            string spokeDate = dict.ContainsKey("SPOKE_DATE") ? dict["SPOKE_DATE"] : "";
            string companyId = dict.ContainsKey("COMPANY_ID") ? dict["COMPANY_ID"] : "";
            string skey = dict.ContainsKey("skey") ? dict["skey"] : "";

            var query = $"SEQ_NO={seqNo}&SPOKE_TIME={spokeTime}&SPOKE_DATE={spokeDate}&COMPANY_ID={companyId}&skey={skey}";
            return $"https://mops.twse.com.tw/mops/web/t05sr01_1_detail?{query}";
        }
    }
}
