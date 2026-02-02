using MvcWebCrawler.Data;
using MvcWebCrawler.Helpers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;

namespace MvcWebCrawler.Services
{
    /// <summary>
    /// ROE 背景蒐集服務
    /// 每小時執行一次，批次蒐集所有股票的 ROE 資料
    /// </summary>
    public class RoeCollectionService : IRegisteredObject
    {
        private const string FINMIND_API_KEY = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJkYXRlIjoiMjAyNi0wMS0yNyAxNTo1MDozNCIsInVzZXJfaWQiOiJtZWxsb3cwMDk5IiwiZW1haWwiOiJnb29kMjk1NjAwMEBnbWFpbC5jb20iLCJpcCI6IjE0MC4xMTIuMTYxLjE1MSJ9.2OReFT0f2eQj0jwG2G4lluFihFNi36CHHZADF-YHJSw";
        
        private const int API_CALLS_PER_HOUR = 300;
        private const int API_DELAY_MS = 12000; // 300 calls/hour = 1 call every 12 seconds
        
        private static readonly object _lock = new object();
        private static Timer _timer;
        private static bool _isRunning = false;
        private readonly RoeRepository _roeRepository;

        public RoeCollectionService()
        {
            _roeRepository = new RoeRepository();
            HostingEnvironment.RegisterObject(this);
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_timer == null)
                {
                    // 每小時執行一次（3600000 毫秒 = 1 小時）
                    _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromHours(1));
                    System.Diagnostics.Debug.WriteLine("[ROE Service] Started at " + DateTime.Now);
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (_timer != null)
                {
                    _timer.Dispose();
                    _timer = null;
                    System.Diagnostics.Debug.WriteLine("[ROE Service] Stopped at " + DateTime.Now);
                }
            }

            HostingEnvironment.UnregisterObject(this);
        }

        private async void DoWork(object state)
        {
            // 確保同時只有一個工作在執行
            if (_isRunning)
            {
                System.Diagnostics.Debug.WriteLine("[ROE Service] Previous task still running, skipping...");
                return;
            }

            _isRunning = true;

            try
            {
                // 更新最後執行時間
                RoeServiceManager.UpdateLastExecutionTime();
                
                System.Diagnostics.Debug.WriteLine($"========================================");
                System.Diagnostics.Debug.WriteLine($"[ROE Service] Starting batch collection at {DateTime.Now}");
                System.Diagnostics.Debug.WriteLine($"========================================");

                await CollectAllStockRoeAsync();

                System.Diagnostics.Debug.WriteLine($"========================================");
                System.Diagnostics.Debug.WriteLine($"[ROE Service] Completed batch collection at {DateTime.Now}");
                System.Diagnostics.Debug.WriteLine($"========================================");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ROE Service] ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        private async Task CollectAllStockRoeAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(5);

                    // 取得所有股票代號
                    var stockList = await GetAllStockIdsAsync(client);
                    System.Diagnostics.Debug.WriteLine($"[ROE Service] Total stocks to process: {stockList.Count}");

                    // 篩選出尚未有快取的股票（這樣清除快取後會重頭開始）
                    var stocksToProcess = new List<string>();
                    foreach (var stockId in stockList)
                    {
                        if (!_roeRepository.HasRoeCache(stockId))
                        {
                            stocksToProcess.Add(stockId);
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[ROE Service] Stocks without cache: {stocksToProcess.Count}");
                    System.Diagnostics.Debug.WriteLine($"[ROE Service] Stocks with cache: {stockList.Count - stocksToProcess.Count}");
                    
                    // 每檔股票需要 12 次 API 呼叫（4個季度 × 3次）
                    // API 限制：300 次/小時
                    // 每小時最多處理：300 ÷ 12 = 25 檔股票
                    int maxStocksPerHour = 25;
                    System.Diagnostics.Debug.WriteLine($"[ROE Service] Will process {Math.Min(maxStocksPerHour, stocksToProcess.Count)} stocks this hour");

                    var stocksToProcessThisHour = stocksToProcess.Take(maxStocksPerHour).ToList();

                    int processedCount = 0;
                    int successCount = 0;
                    int failedCount = 0;

                    foreach (var stockId in stocksToProcessThisHour)
                    {
                        try
                        {
                            processedCount++;
                            System.Diagnostics.Debug.WriteLine($"[ROE Service] Processing {processedCount}/{stocksToProcessThisHour.Count}: {stockId}");

                            var roeData = await FetchStockRoeDataAsync(client, stockId);

                            // 只要有任何一個季度的 ROE 資料就儲存
                            if (roeData.Q1_ROE != "N/A" || roeData.Q2_ROE != "N/A" || 
                                roeData.Q3_ROE != "N/A" || roeData.Q4_ROE != "N/A")
                            {
                                _roeRepository.SaveRoe(stockId, roeData);
                                successCount++;
                                System.Diagnostics.Debug.WriteLine($"[ROE Service] ? Saved ROE for {stockId}");
                            }
                            else
                            {
                                failedCount++;
                                System.Diagnostics.Debug.WriteLine($"[ROE Service] ? No ROE data for {stockId}");
                            }

                            // 延遲以符合 API 限制（每小時 300 次 = 每 12 秒 1 次）
                            await Task.Delay(API_DELAY_MS);
                        }
                        catch (Exception ex)
                        {
                            failedCount++;
                            System.Diagnostics.Debug.WriteLine($"[ROE Service] ERROR processing {stockId}: {ex.Message}");
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[ROE Service] Summary:");
                    System.Diagnostics.Debug.WriteLine($"  - Processed: {processedCount}");
                    System.Diagnostics.Debug.WriteLine($"  - Success: {successCount}");
                    System.Diagnostics.Debug.WriteLine($"  - Failed: {failedCount}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ROE Service] FATAL ERROR: {ex.Message}");
                throw;
            }
        }

        private async Task<List<string>> GetAllStockIdsAsync(HttpClient client)
        {
            var stockIds = new List<string>();

            try
            {
                // 取得上市股票代號
                string twseUrl = "https://openapi.twse.com.tw/v1/opendata/t187ap03_L";
                var twseResponse = await client.GetStringAsync(twseUrl);
                var twseArray = JArray.Parse(twseResponse);

                foreach (var item in twseArray)
                {
                    string stockId = item["公司代號"]?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(stockId))
                    {
                        stockIds.Add(stockId);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[ROE Service] Loaded {stockIds.Count} TWSE stocks");

                // 取得上櫃股票代號
                string tpexUrl = "https://www.tpex.org.tw/openapi/v1/mopsfin_t187ap03_O";
                var tpexResponse = await client.GetStringAsync(tpexUrl);
                var tpexArray = JArray.Parse(tpexResponse);

                int tpexCount = 0;
                foreach (var item in tpexArray)
                {
                    string stockId = item["SecuritiesCompanyCode"]?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(stockId))
                    {
                        stockIds.Add(stockId);
                        tpexCount++;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[ROE Service] Loaded {tpexCount} TPEx stocks");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ROE Service] ERROR getting stock IDs: {ex.Message}");
            }

            return stockIds.Distinct().ToList();
        }

        private async Task<RoeData> FetchStockRoeDataAsync(HttpClient client, string stockId)
        {
            var roeData = new RoeData
            {
                StockId = stockId
            };

            try
            {
                // 取得往回推算的 4 個季度
                var quarters = QuarterHelper.GetLast4QuartersEndDates();
                
                System.Diagnostics.Debug.WriteLine($"[ROE Service] Fetching 4 quarters ROE for: {stockId}");

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
                        previousQuarter = quarters[i + 1];
                    }
                    else
                    {
                        previousQuarter = QuarterHelper.GetPreviousQuarterEndDate(currentQuarter);
                    }
                    
                    var previousQuarterDate = QuarterHelper.ToApiDateString(previousQuarter);

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

                            System.Diagnostics.Debug.WriteLine($"[ROE Service] {currentQuarterLabel}: {roe:F2}%");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ROE Service] ERROR fetching {currentQuarterLabel} for {stockId}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ROE Service] ERROR in FetchStockRoeDataAsync for {stockId}: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"[ROE Service] ERROR in FetchNetIncomeAsync: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"[ROE Service] ERROR in FetchEquityAsync: {ex.Message}");
            }

            return equity;
        }

        public void Stop(bool immediate)
        {
            Stop();
        }
    }
}
