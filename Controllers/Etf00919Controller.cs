using MvcWebCrawler.Models;
using MvcWebCrawler.Data;
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
        // FinMind API 芲
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

            // 眔е参璸
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
                    // ==================== カ布 ====================
                    
                    System.Diagnostics.Debug.WriteLine("Fetching TWSE (カ) stock shares data...");
                    string twseSharesApiUrl = "https://openapi.twse.com.tw/v1/opendata/t187ap03_L";
                    var twseSharesResponse = await client.GetStringAsync(twseSharesApiUrl);
                    var twseSharesArray = JArray.Parse(twseSharesResponse);
                    
                    var twseSharesDict = new Dictionary<string, decimal>();
                    foreach (var item in twseSharesArray)
                    {
                        string stockId = item["そ腹"]?.ToString()?.Trim() ?? "";
                        string sharesStr = item["祇︽炊硄计┪TDR祇︽计"]?.ToString()?.Replace(",", "") ?? "0";
                        
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

                    // 矪瞶カ布
                    foreach (var item in twsePriceArray)
                    {
                        string stockId = item["Code"]?.ToString()?.Trim() ?? "";
                        string stockName = item["Name"]?.ToString()?.Trim() ?? "";
                        string closePriceStr = item["ClosingPrice"]?.ToString()?.Replace(",", "") ?? "0";
                        
                        if (twseSharesDict.ContainsKey(stockId) && decimal.TryParse(closePriceStr, out decimal closePrice))
                        {
                            decimal shares = twseSharesDict[stockId];
                            decimal marketValue = (closePrice * shares) / 100000000m;
                            
                            // 眔 2025 Q2 ROEㄏノе
                            var roeData = await GetStockRoeWithCacheAsync(client, stockId);
                            
                            var etfStock = new Etf00919
                            {
                                StockId = stockId,
                                StockName = stockName,
                                MarketValue = marketValue.ToString("F2"),
                                ClosePrice = closePrice.ToString("F2"),
                                Change = item["Change"]?.ToString() ?? "0",
                                ChangePercent = "0",
                                Volume = item["TradeVolume"]?.ToString() ?? "0",
                                Market = "カ",
                                Industry = "",
                                RoeQuarters = new List<string> { roeData.Q2_2025, "N/A", "N/A", "N/A" },
                                AverageRoe = roeData.Q2_2025,
                                UpdateDate = DateTime.Now.ToString("yyyy-MM-dd")
                            };
                            
                            result.Add(etfStock);
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"TWSE stocks completed. Count: {result.Count}");

                    // ==================== 耫布 ====================
                    
                    System.Diagnostics.Debug.WriteLine("Fetching TPEx (耫) stock shares data...");
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

                        // 矪瞶耫布
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
                                
                                // 眔 2025 Q2 ROEㄏノе
                                var roeData = await GetStockRoeWithCacheAsync(client, stockId);
                                
                                var etfStock = new Etf00919
                                {
                                    StockId = stockId,
                                    StockName = stockName,
                                    MarketValue = marketValue.ToString("F2"),
                                    ClosePrice = closePrice.ToString("F2"),
                                    Change = changeStr,
                                    ChangePercent = "0",
                                    Volume = volumeStr,
                                    Market = "耫",
                                    Industry = "",
                                    RoeQuarters = new List<string> { roeData.Q2_2025, "N/A", "N/A", "N/A" },
                                    AverageRoe = roeData.Q2_2025,
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
                    
                    // ==================== 縵匡籔逼 ====================
                    
                    if (market == "listed")
                    {
                        result = result.Where(x => x.Market == "カ").ToList();
                    }
                    else if (market == "otc")
                    {
                        result = result.Where(x => x.Market == "耫").ToList();
                    }

                    result = result
                        .OrderByDescending(x => decimal.TryParse(x.MarketValue, out decimal val) ? val : 0)
                        .Take(topN)
                        .ToList();

                    for (int i = 0; i < result.Count; i++)
                    {
                        result[i].Rank = i + 1;
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

        // 眔布 2025 Q2 ROEㄏノе诀
        private async Task<RoeData> GetStockRoeWithCacheAsync(HttpClient client, string stockId)
        {
            // 1. 浪琩е
            if (_roeRepository.HasRoeCache(stockId))
            {
                var cachedRoe = _roeRepository.GetRoe(stockId);
                if (cachedRoe != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[CACHE HIT] ROE for {stockId}: {cachedRoe.Q2_2025}%");
                    return cachedRoe;
                }
            }

            // 2. еゼ㏑い眖 API 眔
            System.Diagnostics.Debug.WriteLine($"[CACHE MISS] Fetching ROE from API for {stockId}");
            var roeData = await GetStockRoeFromApiAsync(client, stockId);

            // 3. 纗е
            if (roeData.Q2_2025 != "N/A")
            {
                _roeRepository.SaveRoe(stockId, roeData);
            }

            return roeData;
        }

        // 眖 API 眔布 2025 Q2 ROE
        private async Task<RoeData> GetStockRoeFromApiAsync(HttpClient client, string stockId)
        {
            var roeData = new RoeData
            {
                StockId = stockId,
                Q2_2025 = "N/A",
                NetIncome = 0,
                Equity = 0
            };

            try
            {
                // ㏕﹚琩高 2025 Q2 (2025-06-30)
                string targetDate = "2025-06-30";
                
                System.Diagnostics.Debug.WriteLine($"========================================");
                System.Diagnostics.Debug.WriteLine($"Fetching ROE for stock: {stockId}");
                System.Diagnostics.Debug.WriteLine($"Target date: {targetDate}");
                
                decimal netIncome = 0;
                decimal equity = 0;

                // 眔穕痲戈セ戳瞓- ㄏノ token 把计
                string incomeApiUrl = $"https://api.finmindtrade.com/api/v4/data?dataset=TaiwanStockFinancialStatements&data_id={stockId}&start_date={targetDate}&end_date={targetDate}&token={FINMIND_API_KEY}";
                System.Diagnostics.Debug.WriteLine($"Income API URL: {incomeApiUrl}");
                
                var incomeResponse = await client.GetStringAsync(incomeApiUrl);
                var incomeJson = JObject.Parse(incomeResponse);

                System.Diagnostics.Debug.WriteLine($"Income API Status: {incomeJson["status"]?.ToString()}");
                System.Diagnostics.Debug.WriteLine($"Income API Message: {incomeJson["msg"]?.ToString()}");
                
                if (incomeJson["status"]?.ToString() == "200" && incomeJson["data"] != null)
                {
                    var dataArray = incomeJson["data"] as JArray;
                    System.Diagnostics.Debug.WriteLine($"Income data count: {dataArray?.Count ?? 0}");
                    
                    if (dataArray != null && dataArray.Count > 0)
                    {
                        // 陪ボ┮Τノ戈
                        System.Diagnostics.Debug.WriteLine($"Available income data for {stockId}:");
                        foreach (var item in dataArray)
                        {
                            string date = item["date"]?.ToString() ?? "";
                            string type = item["type"]?.ToString() ?? "";
                            string valueStr = item["value"]?.ToString() ?? "0";
                            System.Diagnostics.Debug.WriteLine($"  Date: {date}, Type: {type}, Value: {valueStr}");
                            
                            // セ戳瞓
                            if (date == targetDate && (type == "祙瞓" || type == "セ戳瞓" || type == "net_income"))
                            {
                                if (decimal.TryParse(valueStr, out decimal income))
                                {
                                    netIncome = income;
                                    System.Diagnostics.Debug.WriteLine($"??? FOUND NET INCOME for {stockId}: {income:N2}");
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"??? NO income data returned for {stockId}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"??? Income API failed for {stockId}");
                }

                // 眔戈玻璽杜戈狥舦痲- ㄏノ token 把计
                string equityApiUrl = $"https://api.finmindtrade.com/api/v4/data?dataset=TaiwanStockBalanceSheet&data_id={stockId}&start_date={targetDate}&end_date={targetDate}&token={FINMIND_API_KEY}";
                System.Diagnostics.Debug.WriteLine($"Equity API URL: {equityApiUrl}");
                
                var equityResponse = await client.GetStringAsync(equityApiUrl);
                var equityJson = JObject.Parse(equityResponse);

                System.Diagnostics.Debug.WriteLine($"Equity API Status: {equityJson["status"]?.ToString()}");
                System.Diagnostics.Debug.WriteLine($"Equity API Message: {equityJson["msg"]?.ToString()}");
                
                if (equityJson["status"]?.ToString() == "200" && equityJson["data"] != null)
                {
                    var dataArray = equityJson["data"] as JArray;
                    System.Diagnostics.Debug.WriteLine($"Equity data count: {dataArray?.Count ?? 0}");
                    
                    if (dataArray != null && dataArray.Count > 0)
                    {
                        // 陪ボ┮Τノ戈
                        System.Diagnostics.Debug.WriteLine($"Available equity data for {stockId}:");
                        foreach (var item in dataArray)
                        {
                            string date = item["date"]?.ToString() ?? "";
                            string type = item["type"]?.ToString() ?? "";
                            string valueStr = item["value"]?.ToString() ?? "0";
                            System.Diagnostics.Debug.WriteLine($"  Date: {date}, Type: {type}, Value: {valueStr}");
                            
                            // 狥舦痲
                            if (date == targetDate && (type == "舦痲羆肂" || type == "狥舦痲" || type == "equity"))
                            {
                                if (decimal.TryParse(valueStr, out decimal eq))
                                {
                                    equity = eq;
                                    System.Diagnostics.Debug.WriteLine($"??? FOUND EQUITY for {stockId}: {eq:N2}");
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"??? NO equity data returned for {stockId}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"??? Equity API failed for {stockId}");
                }

                // 璸衡 ROE
                System.Diagnostics.Debug.WriteLine($"Final values for {stockId}:");
                System.Diagnostics.Debug.WriteLine($"  Net Income: {netIncome:N2}");
                System.Diagnostics.Debug.WriteLine($"  Equity: {equity:N2}");
                
                if (netIncome != 0 && equity > 0)
                {
                    decimal roe = (netIncome / equity) * 100;
                    roeData.Q2_2025 = roe.ToString("F2");
                    roeData.NetIncome = netIncome;
                    roeData.Equity = equity;
                    System.Diagnostics.Debug.WriteLine($"??? CALCULATED ROE for {stockId}: {roe:F2}%");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"??? CANNOT CALCULATE ROE for {stockId}");
                    System.Diagnostics.Debug.WriteLine($"    Reason: NetIncome={netIncome}, Equity={equity}");
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

        // Αてカ货じ
        public static string FormatMarketValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            
            if (decimal.TryParse(value, out decimal number))
            {
                return number.ToString("N2") + " 货";
            }
            return value;
        }

        // Αて计
        public static string FormatNumber(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            
            if (decimal.TryParse(value, out decimal number))
            {
                return number.ToString("N0");
            }
            return value;
        }

        // Αて基计翴ㄢ
        public static string FormatPrice(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            
            if (decimal.TryParse(value, out decimal number))
            {
                return number.ToString("N2");
            }
            return value;
        }

        // Αてκだゑ
        public static string FormatPercent(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            
            if (decimal.TryParse(value, out decimal number))
            {
                return number.ToString("N2") + "%";
            }
            return value;
        }

        // 耞害禴肅︹
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
