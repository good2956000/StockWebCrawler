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
    public class MarketCapController : Controller
    {
        // GET: MarketCap
        public async Task<ActionResult> Index(string market = "all", int topN = 50)
        {
            ViewBag.SelectedMarket = market;
            ViewBag.TopN = topN;

            var marketCapData = await GetMarketCapDataAsync(market, topN);

            return View(marketCapData);
        }

        private async Task<List<MarketCap>> GetMarketCapDataAsync(string market, int topN)
        {
            var result = new List<MarketCap>();
            
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // ==================== 上市股票 ====================
                    
                    // 步驟 1: 取得上市已發行股數資料
                    System.Diagnostics.Debug.WriteLine("Fetching TWSE (上市) stock shares data...");
                    string twseSharesApiUrl = "https://openapi.twse.com.tw/v1/opendata/t187ap03_L";
                    var twseSharesResponse = await client.GetStringAsync(twseSharesApiUrl);
                    var twseSharesArray = JArray.Parse(twseSharesResponse);
                    
                    // 建立上市股數字典
                    var twseSharesDict = new Dictionary<string, decimal>();
                    foreach (var item in twseSharesArray)
                    {
                        string stockId = item["公司代號"]?.ToString()?.Trim() ?? "";
                        string sharesStr = item["已發行普通股數或TDR原股發行股數"]?.ToString()?.Replace(",", "") ?? "0";
                        
                        if (!string.IsNullOrEmpty(stockId) && decimal.TryParse(sharesStr, out decimal shares))
                        {
                            twseSharesDict[stockId] = shares;
                        }
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Loaded {twseSharesDict.Count} TWSE stocks shares data");

                    // 步驟 2: 取得上市個股日收盤價
                    System.Diagnostics.Debug.WriteLine("Fetching TWSE (上市) stock prices data...");
                    string twsePriceApiUrl = "https://openapi.twse.com.tw/v1/exchangeReport/STOCK_DAY_AVG_ALL";
                    var twsePriceResponse = await client.GetStringAsync(twsePriceApiUrl);
                    var twsePriceArray = JArray.Parse(twsePriceResponse);
                    
                    System.Diagnostics.Debug.WriteLine($"Loaded {twsePriceArray.Count} TWSE stocks price data");

                    // 步驟 3: 計算上市股票市值
                    foreach (var item in twsePriceArray)
                    {
                        string stockId = item["Code"]?.ToString()?.Trim() ?? "";
                        string stockName = item["Name"]?.ToString()?.Trim() ?? "";
                        string closePriceStr = item["ClosingPrice"]?.ToString()?.Replace(",", "") ?? "0";
                        
                        if (twseSharesDict.ContainsKey(stockId) && decimal.TryParse(closePriceStr, out decimal closePrice))
                        {
                            decimal shares = twseSharesDict[stockId];
                            decimal marketValue = (closePrice * shares) / 100000000m;
                            
                            var marketCap = new MarketCap
                            {
                                StockId = stockId,
                                StockName = stockName,
                                MarketValue = marketValue.ToString("F2"),
                                ClosePrice = closePrice.ToString("F2"),
                                Change = item["Change"]?.ToString() ?? "0",
                                ChangePercent = "0",
                                Volume = item["TradeVolume"]?.ToString() ?? "0",
                                Market = "上市",
                                Industry = "",
                                UpdateDate = DateTime.Now.ToString("yyyy-MM-dd")
                            };
                            
                            result.Add(marketCap);
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"TWSE market cap completed. Count: {result.Count}");

                    // ==================== 上櫃股票 ====================
                    
                    // 步驟 4: 取得上櫃已發行股數資料
                    System.Diagnostics.Debug.WriteLine("Fetching TPEx (上櫃) stock shares data...");
                    string tpexSharesApiUrl = "https://www.tpex.org.tw/openapi/v1/mopsfin_t187ap03_O";
                    
                    try
                    {
                        var tpexSharesResponse = await client.GetStringAsync(tpexSharesApiUrl);
                        System.Diagnostics.Debug.WriteLine($"TPEx shares API response length: {tpexSharesResponse.Length}");
                        
                        var tpexSharesArray = JArray.Parse(tpexSharesResponse);
                        
                        // 建立上櫃股數字典
                        var tpexSharesDict = new Dictionary<string, decimal>();
                        foreach (var item in tpexSharesArray)
                        {
                            string stockId = item["SecuritiesCompanyCode"]?.ToString()?.Trim() ?? "";
                            string sharesStr = item["IssueShares"]?.ToString()?.Replace(",", "") ?? "0";
                            
                            if (!string.IsNullOrEmpty(stockId) && decimal.TryParse(sharesStr, out decimal shares))
                            {
                                tpexSharesDict[stockId] = shares;
                            }
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"Loaded {tpexSharesDict.Count} TPEx stocks shares data");

                        // 步驟 5: 取得上櫃收盤行情
                        System.Diagnostics.Debug.WriteLine("Fetching TPEx (上櫃) stock prices data...");
                        string tpexPriceApiUrl = "https://www.tpex.org.tw/openapi/v1/tpex_mainboard_quotes";
                        
                        var tpexPriceResponse = await client.GetStringAsync(tpexPriceApiUrl);
                        System.Diagnostics.Debug.WriteLine($"TPEx prices API response length: {tpexPriceResponse.Length}");
                        
                        var tpexPriceArray = JArray.Parse(tpexPriceResponse);
                        
                        System.Diagnostics.Debug.WriteLine($"Loaded {tpexPriceArray.Count} TPEx stocks price data");

                        // 檢查第一筆資料的欄位名稱
                        if (tpexPriceArray.Count > 0)
                        {
                            var firstItem = tpexPriceArray[0];
                            System.Diagnostics.Debug.WriteLine($"TPEx first item: {firstItem["SecuritiesCompanyCode"]} - {firstItem["CompanyName"]}");
                        }

                        // 步驟 6: 計算上櫃股票市值
                        int tpexAddedCount = 0;
                        foreach (var item in tpexPriceArray)
                        {
                            string stockId = item["SecuritiesCompanyCode"]?.ToString()?.Trim() ?? "";
                            string stockName = item["CompanyName"]?.ToString()?.Trim() ?? "";
                            string closePriceStr = item["Close"]?.ToString()?.Replace(",", "").Replace("+", "").Replace("-", "") ?? "0";
                            
                            if (!string.IsNullOrEmpty(stockId) && tpexSharesDict.ContainsKey(stockId) && decimal.TryParse(closePriceStr, out decimal closePrice) && closePrice > 0)
                            {
                                decimal shares = tpexSharesDict[stockId];
                                decimal marketValue = (closePrice * shares) / 100000000m;
                                
                                // 處理漲跌（需要移除 + 或 - 符號）
                                string changeStr = item["Change"]?.ToString()?.Trim() ?? "0";
                                // 移除 "+" 符號，保留 "-"
                                if (changeStr.StartsWith("+"))
                                {
                                    changeStr = changeStr.Substring(1);
                                }
                                
                                string volumeStr = item["TradingShares"]?.ToString() ?? "0";
                                
                                var marketCap = new MarketCap
                                {
                                    StockId = stockId,
                                    StockName = stockName,
                                    MarketValue = marketValue.ToString("F2"),
                                    ClosePrice = closePrice.ToString("F2"),
                                    Change = changeStr,
                                    ChangePercent = "0",
                                    Volume = volumeStr,
                                    Market = "上櫃",
                                    Industry = "",
                                    UpdateDate = DateTime.Now.ToString("yyyy-MM-dd")
                                };
                                
                                result.Add(marketCap);
                                tpexAddedCount++;
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"TPEx market cap completed. Added: {tpexAddedCount}, Total count: {result.Count}");
                    }
                    catch (Exception tpexEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error fetching TPEx data: {tpexEx.Message}");
                        System.Diagnostics.Debug.WriteLine($"TPEx Stack trace: {tpexEx.StackTrace}");
                    }
                    // ==================== 篩選與排序 ====================
                    
                    // 步驟 7: 根據市場別篩選
                    if (market == "listed")
                    {
                        result = result.Where(x => x.Market == "上市").ToList();
                    }
                    else if (market == "otc")
                    {
                        result = result.Where(x => x.Market == "上櫃").ToList();
                    }
                    // market == "all" 則不篩選

                    // 步驟 8: 按市值排序並取前 N 名
                    result = result
                        .OrderByDescending(x => decimal.TryParse(x.MarketValue, out decimal val) ? val : 0)
                        .Take(topN)
                        .ToList();

                    // 步驟 9: 設定排名
                    for (int i = 0; i < result.Count; i++)
                    {
                        result[i].Rank = i + 1;
                    }

                    System.Diagnostics.Debug.WriteLine($"Market cap calculation completed. Final result: {result.Count} stocks");
                    System.Diagnostics.Debug.WriteLine($"Market filter: {market}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching market cap data: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            return result;
        }

        // 格式化市值（億元）
        public static string FormatMarketValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            
            if (decimal.TryParse(value, out decimal number))
            {
                return number.ToString("N2") + " 億";
            }
            return value;
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

        // 格式化百分比
        public static string FormatPercent(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            
            if (decimal.TryParse(value, out decimal number))
            {
                return number.ToString("N2") + "%";
            }
            return value;
        }

        // 判斷漲跌顏色
        public static string GetChangeColor(string change)
        {
            if (string.IsNullOrEmpty(change)) return "";
            
            if (decimal.TryParse(change, out decimal value))
            {
                if (value > 0)
                    return "text-danger fw-bold"; // 紅色 - 上漲
                else if (value < 0)
                    return "text-success fw-bold"; // 綠色 - 下跌
            }
            
            return ""; // 平盤
        }

        // GET: MarketCap/TestTPExAPI (測試用)
        public async Task<ActionResult> TestTPExAPI()
        {
            var result = new List<string>();
            
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // 測試上櫃股數 API
                    result.Add("=== 測試上櫃股數 API ===");
                    string sharesUrl = "https://www.tpex.org.tw/openapi/v1/mopsfin_t187ap03_O";
                    var sharesResponse = await client.GetStringAsync(sharesUrl);
                    result.Add($"Response Length: {sharesResponse.Length}");
                    result.Add($"First 500 chars: {sharesResponse.Substring(0, Math.Min(500, sharesResponse.Length))}");
                    
                    var sharesArray = JArray.Parse(sharesResponse);
                    result.Add($"Array Count: {sharesArray.Count}");
                    
                    if (sharesArray.Count > 0)
                    {
                        result.Add("First Item:");
                        result.Add(sharesArray[0].ToString());
                    }
                    
                    result.Add("");
                    result.Add("=== 測試上櫃價格 API ===");
                    string priceUrl = "https://www.tpex.org.tw/openapi/v1/tpex_mainboard_quotes";
                    var priceResponse = await client.GetStringAsync(priceUrl);
                    result.Add($"Response Length: {priceResponse.Length}");
                    result.Add($"First 500 chars: {priceResponse.Substring(0, Math.Min(500, priceResponse.Length))}");
                    
                    var priceArray = JArray.Parse(priceResponse);
                    result.Add($"Array Count: {priceArray.Count}");
                    
                    if (priceArray.Count > 0)
                    {
                        result.Add("First Item:");
                        result.Add(priceArray[0].ToString());
                        
                        result.Add("");
                        result.Add("Available Fields:");
                        var fields = priceArray[0].Children<JProperty>().Select(p => p.Name);
                        result.Add(string.Join(", ", fields));
                    }
                }
            }
            catch (Exception ex)
            {
                result.Add($"Error: {ex.Message}");
                result.Add($"Stack Trace: {ex.StackTrace}");
            }
            
            ViewBag.TestResults = result;
            return View();
        }
    }
}
