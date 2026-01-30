using MvcWebCrawler.Models;
using MvcWebCrawler.Data;
using MvcWebCrawler.Helpers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace MvcWebCrawler.Controllers
{
    public class Etf00919Controller : Controller
    {
        // FinMind API 金鑰
        private const string FINMIND_API_KEY = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJkYXRlIjoiMjAyNi0wMS0yNyAxNTo1MDozNCIsInVzZXJfaWQiOiJtZWxsb3cwMDk5IiwiZW1haWwiOiJnb29kMjk1NjAwMEBnbWFpbC5jb20iLCJpcCI6IjE0MC4xMTIuMTYxLjE1MSJ9.2OReFT0f2eQj0jwG2G4lluFihFNi36CHHZADF-YHJSw";

        private readonly RoeRepository _roeRepository;

        public Etf00919Controller()
        {
            _roeRepository = new RoeRepository();
        }

        // GET: Etf00919
        public async Task<ActionResult> Index(string market = "all", int topN = 10)
        {
            ViewBag.SelectedMarket = market;
            ViewBag.TopN = topN;

            var etfData = await GetEtf00919DataAsync(market, topN);

            // 取得快取統計
            var cacheStats = _roeRepository.GetCacheStats();
            ViewBag.RoeCacheCount = cacheStats.Count;
            ViewBag.RoeCacheLastUpdate = cacheStats.LastUpdate;

            return View(etfData);
        }

        private async Task<List<Etf00919>> GetEtf00919DataAsync(string market, int topN)
        {
            var result = new List<Etf00919>();
            
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // ==================== 上市股票 ====================
                    
                    System.Diagnostics.Debug.WriteLine("Fetching TWSE (上市) stock shares data...");
                    string twseSharesApiUrl = "https://openapi.twse.com.tw/v1/opendata/t187ap03_L";
                    var twseSharesResponse = await client.GetStringAsync(twseSharesApiUrl);
                    var twseSharesArray = JArray.Parse(twseSharesResponse);
                    
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

                    string twsePriceApiUrl = "https://openapi.twse.com.tw/v1/exchangeReport/STOCK_DAY_AVG_ALL";
                    var twsePriceResponse = await client.GetStringAsync(twsePriceApiUrl);
                    var twsePriceArray = JArray.Parse(twsePriceResponse);
                    
                    System.Diagnostics.Debug.WriteLine($"Loaded {twsePriceArray.Count} TWSE stocks price data");

                    // 處理上市股票（先不計算 ROE）
                    foreach (var item in twsePriceArray)
                    {
                        string stockId = item["Code"]?.ToString()?.Trim() ?? "";
                        string stockName = item["Name"]?.ToString()?.Trim() ?? "";
                        string closePriceStr = item["ClosingPrice"]?.ToString()?.Replace(",", "") ?? "0";
                        
                        if (twseSharesDict.ContainsKey(stockId) && decimal.TryParse(closePriceStr, out decimal closePrice))
                        {
                            decimal shares = twseSharesDict[stockId];
                            decimal marketValue = (closePrice * shares) / 100000000m;
                            
                            var etfStock = new Etf00919
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
                                RoeQuarters = new List<string> { "N/A", "N/A", "N/A", "N/A" },
                                AverageRoe = "N/A",
                                UpdateDate = DateTime.Now.ToString("yyyy-MM-dd")
                            };
                            
                            result.Add(etfStock);
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"TWSE stocks completed. Count: {result.Count}");

                    // ==================== 上櫃股票 ====================
                    
                    System.Diagnostics.Debug.WriteLine("Fetching TPEx (上櫃) stock shares data...");
                    string tpexSharesApiUrl = "https://www.tpex.org.tw/openapi/v1/mopsfin_t187ap03_O";
                    
                    try
                    {
                        var tpexSharesResponse = await client.GetStringAsync(tpexSharesApiUrl);
                        var tpexSharesArray = JArray.Parse(tpexSharesResponse);
                        
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

                        string tpexPriceApiUrl = "https://www.tpex.org.tw/openapi/v1/tpex_mainboard_quotes";
                        var tpexPriceResponse = await client.GetStringAsync(tpexPriceApiUrl);
                        var tpexPriceArray = JArray.Parse(tpexPriceResponse);
                        
                        System.Diagnostics.Debug.WriteLine($"Loaded {tpexPriceArray.Count} TPEx stocks price data");

                        // 處理上櫃股票（先不計算 ROE）
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
                                
                                string changeStr = item["Change"]?.ToString()?.Trim() ?? "0";
                                if (changeStr.StartsWith("+"))
                                {
                                    changeStr = changeStr.Substring(1);
                                }
                                
                                string volumeStr = item["TradingShares"]?.ToString() ?? "0";
                                
                                var etfStock = new Etf00919
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
                                    RoeQuarters = new List<string> { "N/A", "N/A", "N/A", "N/A" },
                                    AverageRoe = "N/A",
                                    UpdateDate = DateTime.Now.ToString("yyyy-MM-dd")
                                };
                                
                                result.Add(etfStock);
                                tpexAddedCount++;
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"TPEx stocks completed. Added: {tpexAddedCount}, Total count: {result.Count}");
                    }
                    catch (Exception tpexEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error fetching TPEx data: {tpexEx.Message}");
                    }
                    
                    // ==================== 篩選與排序 ====================
                    
                    if (market == "listed")
                    {
                        result = result.Where(x => x.Market == "上市").ToList();
                    }
                    else if (market == "otc")
                    {
                        result = result.Where(x => x.Market == "上櫃").ToList();
                    }

                    // 先按市值排序並取前 N 名
                    result = result
                        .OrderByDescending(x => decimal.TryParse(x.MarketValue, out decimal val) ? val : 0)
                        .Take(topN)
                        .ToList();

                    System.Diagnostics.Debug.WriteLine($"Top {topN} stocks selected. Now calculating ROE...");

                    // ==================== 只對前 N 名人計算 ROE ====================
                    for (int i = 0; i < result.Count; i++)
                    {
                        result[i].Rank = i + 1;
                        
                        // 取得 ROE（使用快取）
                        System.Diagnostics.Debug.WriteLine($"Calculating ROE for rank {i + 1}: {result[i].StockId} {result[i].StockName}");
                        var roeData = await GetStockRoeWithCacheAsync(client, result[i].StockId);
                        
                        result[i].RoeQuarters = new List<string> 
                        { 
                            roeData.Q1_ROE ?? "N/A", 
                            roeData.Q2_ROE ?? "N/A", 
                            roeData.Q3_ROE ?? "N/A", 
                            roeData.Q4_ROE ?? "N/A" 
                        };
                        result[i].AverageRoe = roeData.Q1_ROE ?? "N/A"; // 使用最新季度的 ROE
                    }

                    System.Diagnostics.Debug.WriteLine($"Market cap ranking completed. Final result: {result.Count} stocks (topN={topN})");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching data: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            return result;
        }

        // 取得股票 2025 Q2 ROE（使用快取機制）
        private async Task<RoeData> GetStockRoeWithCacheAsync(HttpClient client, string stockId)
        {
            // 1. 先檢查快取
            if (_roeRepository.HasRoeCache(stockId))
            {
                var cachedRoe = _roeRepository.GetRoe(stockId);
                if (cachedRoe != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[CACHE HIT] ROE for {stockId}: {cachedRoe.Q2_2025}%");
                    return cachedRoe;
                }
            }

            // 2. 快取未命中，從 API 取得
            System.Diagnostics.Debug.WriteLine($"[CACHE MISS] Fetching ROE from API for {stockId}");
            var roeData = await GetStockRoeFromApiAsync(client, stockId);

            // 3. 儲存到快取（只要有任何一個季度的資料就儲存）
            if (roeData.Q1_ROE != "N/A" || roeData.Q2_ROE != "N/A" || 
                roeData.Q3_ROE != "N/A" || roeData.Q4_ROE != "N/A")
            {
                _roeRepository.SaveRoe(stockId, roeData);
            }

            return roeData;
        }

        // 從 API 取得股票 ROE (使用近兩季股東權益平均，根據當前時間自動計算 4 個季度)
        private async Task<RoeData> GetStockRoeFromApiAsync(HttpClient client, string stockId)
        {
            var roeData = new RoeData
            {
                StockId = stockId
            };

            try
            {
                // 取得往回推算的 4 個季度
                var quarters = QuarterHelper.GetLast4QuartersEndDates();
                
                System.Diagnostics.Debug.WriteLine($"========================================");
                System.Diagnostics.Debug.WriteLine($"Fetching ROE for stock: {stockId}");
                System.Diagnostics.Debug.WriteLine($"Calculating 4 quarters ROE...");

                // 逐一計算 4 個季度的 ROE
                for (int i = 0; i < quarters.Length; i++)
                {
                    var currentQuarter = quarters[i];
                    var currentQuarterDate = QuarterHelper.ToApiDateString(currentQuarter);
                    var currentQuarterLabel = QuarterHelper.GetQuarterLabel(currentQuarter);
                    var quarterNumber = QuarterHelper.GetQuarterNumber(currentQuarter);

                    // 需要上一季的權益來計算平均
                    DateTime previousQuarter;
                    if (i < quarters.Length - 1)
                    {
                        previousQuarter = quarters[i + 1]; // 使用數組中的下一個（更早的季度）
                    }
                    else
                    {
                        previousQuarter = QuarterHelper.GetPreviousQuarterEndDate(currentQuarter);
                    }
                    
                    var previousQuarterDate = QuarterHelper.ToApiDateString(previousQuarter);
                    var previousQuarterLabel = QuarterHelper.GetQuarterLabel(previousQuarter);

                    System.Diagnostics.Debug.WriteLine($"Quarter {i + 1}: {currentQuarterLabel}");
                    System.Diagnostics.Debug.WriteLine($"  Current: {currentQuarterDate}");
                    System.Diagnostics.Debug.WriteLine($"  Previous: {previousQuarterDate}");

                    try
                    {
                        // 取得當季稅後淨利
                        decimal netIncome = await FetchNetIncomeAsync(client, stockId, currentQuarterDate);
                        
                        // 取得當季股東權益
                        decimal currentEquity = await FetchEquityAsync(client, stockId, currentQuarterDate);
                        
                        // 取得上一季股東權益
                        decimal previousEquity = await FetchEquityAsync(client, stockId, previousQuarterDate);

                        // 計算 ROE（使用近兩季股東權益平均）
                        if (netIncome != 0 && currentEquity > 0 && previousEquity > 0)
                        {
                            decimal averageEquity = (currentEquity + previousEquity) / 2;
                            decimal roe = (netIncome / averageEquity) * 100;

                            // 根據實際的季度編號儲存到對應的欄位
                            switch (quarterNumber)
                            {
                                case 1: // Q1 (03-31)
                                    roeData.Q1_ROE = roe.ToString("F2");
                                    roeData.Q1_Date = currentQuarterDate;
                                    roeData.NetIncome_Q1 = netIncome;
                                    roeData.Equity_Q1 = averageEquity;
                                    break;
                                case 2: // Q2 (06-30)
                                    roeData.Q2_ROE = roe.ToString("F2");
                                    roeData.Q2_Date = currentQuarterDate;
                                    roeData.NetIncome_Q2 = netIncome;
                                    roeData.Equity_Q2 = averageEquity;
                                    break;
                                case 3: // Q3 (09-30)
                                    roeData.Q3_ROE = roe.ToString("F2");
                                    roeData.Q3_Date = currentQuarterDate;
                                    roeData.NetIncome_Q3 = netIncome;
                                    roeData.Equity_Q3 = averageEquity;
                                    break;
                                case 4: // Q4 (12-31)
                                    roeData.Q4_ROE = roe.ToString("F2");
                                    roeData.Q4_Date = currentQuarterDate;
                                    roeData.NetIncome_Q4 = netIncome;
                                    roeData.Equity_Q4 = averageEquity;
                                    break;
                            }

                            System.Diagnostics.Debug.WriteLine($"  ? ROE: {roe:F2}%");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"  ? Cannot calculate ROE");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"  ? Error: {ex.Message}");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"========================================");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"??? ERROR calculating ROE for {stockId}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            return roeData;
        }

        // 取得稅後淨利
        private async Task<decimal> FetchNetIncomeAsync(HttpClient client, string stockId, string targetDate)
        {
            decimal netIncome = 0;

            try
            {
                string incomeApiUrl = $"https://api.finmindtrade.com/api/v4/data?dataset=TaiwanStockFinancialStatements&data_id={stockId}&start_date={targetDate}&end_date={targetDate}&token={FINMIND_API_KEY}";
                var incomeResponse = await client.GetStringAsync(incomeApiUrl);
                var incomeJson = JObject.Parse(incomeResponse);

                if (incomeJson["status"]?.ToString() == "200" && incomeJson["data"] != null)
                {
                    var dataArray = incomeJson["data"] as JArray;
                    if (dataArray != null && dataArray.Count > 0)
                    {
                        foreach (var item in dataArray)
                        {
                            string date = item["date"]?.ToString() ?? "";
                            string type = item["type"]?.ToString() ?? "";
                            string valueStr = item["value"]?.ToString() ?? "0";

                            if (date == targetDate && type == "IncomeAfterTaxes")
                            {
                                if (decimal.TryParse(valueStr, out decimal income))
                                {
                                    netIncome = income;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching net income: {ex.Message}");
            }

            return netIncome;
        }

        // 取得股東權益
        private async Task<decimal> FetchEquityAsync(HttpClient client, string stockId, string targetDate)
        {
            decimal equity = 0;

            try
            {
                string equityApiUrl = $"https://api.finmindtrade.com/api/v4/data?dataset=TaiwanStockBalanceSheet&data_id={stockId}&start_date={targetDate}&end_date={targetDate}&token={FINMIND_API_KEY}";
                var equityResponse = await client.GetStringAsync(equityApiUrl);
                var equityJson = JObject.Parse(equityResponse);

                if (equityJson["status"]?.ToString() == "200" && equityJson["data"] != null)
                {
                    var dataArray = equityJson["data"] as JArray;
                    if (dataArray != null && dataArray.Count > 0)
                    {
                        foreach (var item in dataArray)
                        {
                            string date = item["date"]?.ToString() ?? "";
                            string type = item["type"]?.ToString() ?? "";
                            string valueStr = item["value"]?.ToString() ?? "0";

                            if (date == targetDate && type == "Equity")
                            {
                                if (decimal.TryParse(valueStr, out decimal eq))
                                {
                                    equity = eq;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching equity: {ex.Message}");
            }

            return equity;
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
                    return "text-danger fw-bold";
                else if (value < 0)
                    return "text-success fw-bold";
            }
            
            return "";
        }
    }
}
