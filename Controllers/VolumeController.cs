using MvcWebCrawler.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace MvcWebCrawler.Controllers
{
    public class VolumeController : Controller
    {
        // FinMind API 金鑰 (請替換成您的金鑰)
        private const string FINMIND_API_KEY = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJkYXRlIjoiMjAyNi0wMS0yNyAxNTo1MDozNCIsInVzZXJfaWQiOiJtZWxsb3cwMDk5IiwiZW1haWwiOiJnb29kMjk1NjAwMEBnbWFpbC5jb20iLCJpcCI6IjE0MC4xMTIuMTYxLjE1MSJ9.2OReFT0f2eQj0jwG2G4lluFihFNi36CHHZADF-YHJSw";

        // GET: Volume
        public async Task<ActionResult> Index(string stockId = "", string startDate = "", string endDate = "")
        {
            // 如果沒有指定日期，使用最近7天
            if (string.IsNullOrEmpty(startDate))
            {
                startDate = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd");
            }
            if (string.IsNullOrEmpty(endDate))
            {
                endDate = DateTime.Now.ToString("yyyy-MM-dd");
            }

            ViewBag.SearchStockId = stockId;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            List<StockVolume> volumeData = new List<StockVolume>();

            // 如果有指定股票代號，則查詢該股票
            if (!string.IsNullOrEmpty(stockId))
            {
                volumeData = await GetStockVolumeAsync(stockId.Trim(), startDate, endDate);
                
                // 計算統計資訊
                if (volumeData != null && volumeData.Count > 0)
                {
                    // 計算總成交金額
                    decimal totalTradingMoney = 0;
                    int validDays = 0;
                    
                    foreach (var item in volumeData)
                    {
                        if (decimal.TryParse(item.Trading_money, out decimal money))
                        {
                            totalTradingMoney += money;
                            validDays++;
                        }
                    }
                    
                    // 計算日平均成交金額
                    decimal avgTradingMoney = validDays > 0 ? totalTradingMoney / validDays : 0;
                    
                    ViewBag.TotalTradingMoney = totalTradingMoney;
                    ViewBag.AverageTradingMoney = avgTradingMoney;
                    ViewBag.TradingDays = validDays;
                }
            }

            return View(volumeData);
        }

        private async Task<List<StockVolume>> GetStockVolumeAsync(string stockId, string startDate, string endDate)
        {
            var result = new List<StockVolume>();
            
            // FinMind API URL
            string apiUrl = $"https://api.finmindtrade.com/api/v4/data?dataset=TaiwanStockPrice&data_id={stockId}&start_date={startDate}&end_date={endDate}";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // 設定 Authorization Header
                    if (!string.IsNullOrEmpty(FINMIND_API_KEY) && FINMIND_API_KEY != "YOUR_API_KEY_HERE")
                    {
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {FINMIND_API_KEY}");
                    }
                    
                    var response = await client.GetStringAsync(apiUrl);
                    var jsonData = JObject.Parse(response);

                    System.Diagnostics.Debug.WriteLine($"API Response: {response}");

                    // 檢查是否有資料
                    if (jsonData["status"]?.ToString() == "200" && jsonData["data"] != null)
                    {
                        var dataArray = jsonData["data"] as JArray;
                        
                        if (dataArray != null && dataArray.Count > 0)
                        {
                            foreach (var item in dataArray)
                            {
                                var volume = new StockVolume
                                {
                                    date = item["date"]?.ToString() ?? "",
                                    stock_id = item["stock_id"]?.ToString() ?? "",
                                    Trading_Volume = item["Trading_Volume"]?.ToString() ?? "",
                                    Trading_money = item["Trading_money"]?.ToString() ?? "",
                                    open = item["open"]?.ToString() ?? "",
                                    max = item["max"]?.ToString() ?? "",
                                    min = item["min"]?.ToString() ?? "",
                                    close = item["close"]?.ToString() ?? "",
                                    spread = item["spread"]?.ToString() ?? "",
                                    Trading_turnover = item["Trading_turnover"]?.ToString() ?? "",
                                    StockName = GetStockName(item["stock_id"]?.ToString() ?? "")
                                };

                                result.Add(volume);
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"API Error: {jsonData["msg"]?.ToString()}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error fetching volume data: {ex.Message}");
                }
            }

            // 按日期降序排列（最新的在前面）
            return result.OrderByDescending(x => x.date).ToList();
        }

        // 簡單的股票名稱對照（可以擴充或從資料庫讀取）
        private string GetStockName(string stockId)
        {
            var stockNames = new Dictionary<string, string>
            {
                { "2330", "台積電" },
                { "2317", "鴻海" },
                { "2454", "聯發科" },
                { "2412", "中華電" },
                { "2308", "台達電" },
                { "1301", "台塑" },
                { "1303", "南亞" },
                { "2882", "國泰金" },
                { "2881", "富邦金" },
                { "2886", "兆豐金" },
                // 可以繼續添加更多...
            };

            return stockNames.ContainsKey(stockId) ? stockNames[stockId] : stockId;
        }

        // 格式化數字
        public static string FormatNumber(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            
            if (decimal.TryParse(value, out decimal number))
            {
                return number.ToString("N0");
            }
            return value;
        }

        // 格式化價格（小數點後兩位）
        public static string FormatPrice(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            
            if (decimal.TryParse(value, out decimal number))
            {
                return number.ToString("N2");
            }
            return value;
        }

        // 判斷漲跌顏色
        public static string GetPriceChangeColor(string spread)
        {
            if (string.IsNullOrEmpty(spread)) return "";
            
            if (decimal.TryParse(spread, out decimal value))
            {
                if (value > 0)
                    return "text-danger fw-bold"; // 紅色 - 上漲
                else if (value < 0)
                    return "text-success fw-bold"; // 綠色 - 下跌
            }
            
            return ""; // 平盤
        }
    }
}
