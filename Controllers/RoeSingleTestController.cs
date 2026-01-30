using MvcWebCrawler.Data;
using MvcWebCrawler.Helpers;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace MvcWebCrawler.Controllers
{
    public class RoeSingleTestController : Controller
    {
        private const string FINMIND_API_KEY = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJkYXRlIjoiMjAyNi0wMS0yNyAxNTo1MDozNCIsInVzZXJfaWQiOiJtZWxsb3cwMDk5IiwiZW1haWwiOiJnb29kMjk1NjAwMEBnbWFpbC5jb20iLCJpcCI6IjE0MC4xMTIuMTYxLjE1MSJ9.2OReFT0f2eQj0jwG2G4lluFihFNi36CHHZADF-YHJSw";

        // GET: RoeSingleTest
        public async Task<ActionResult> Index(string stockId = "2330")
        {
            ViewBag.StockId = stockId;

            if (string.IsNullOrEmpty(stockId))
            {
                return View(new SingleRoeTestResult());
            }

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var result = await TestSingleStockRoeAsync(client, stockId);
                    return View(result);
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                ViewBag.StackTrace = ex.StackTrace;
                return View(new SingleRoeTestResult());
            }
        }

        private async Task<SingleRoeTestResult> TestSingleStockRoeAsync(HttpClient client, string stockId)
        {
            // 自動計算當季和上一季的日期
            var (currentQuarterDate, previousQuarterDate) = QuarterHelper.GetCurrentAndPreviousQuarterDates();
            var currentQuarter = QuarterHelper.GetLatestAvailableQuarterEndDate();
            var currentQuarterLabel = QuarterHelper.GetQuarterLabel(currentQuarter);
            var previousQuarter = QuarterHelper.GetPreviousQuarterEndDate(currentQuarter);
            var previousQuarterLabel = QuarterHelper.GetQuarterLabel(previousQuarter);

            var result = new SingleRoeTestResult
            {
                StockId = stockId,
                TestTime = DateTime.Now,
                CurrentQuarterDate = currentQuarterDate,
                PreviousQuarterDate = previousQuarterDate,
                CurrentQuarterLabel = currentQuarterLabel,
                PreviousQuarterLabel = previousQuarterLabel
            };

            try
            {
                // ==================== 取得當季稅後淨利 ====================
                result.Step1_Description = $"Step 1: 取得當季稅後淨利 ({currentQuarterLabel})";
                string incomeApiUrl = $"https://api.finmindtrade.com/api/v4/data?dataset=TaiwanStockFinancialStatements&data_id={stockId}&start_date={result.CurrentQuarterDate}&end_date={result.CurrentQuarterDate}&token={FINMIND_API_KEY}";
                result.IncomeApiUrl = incomeApiUrl;

                var incomeResponse = await client.GetStringAsync(incomeApiUrl);
                var incomeJson = JObject.Parse(incomeResponse);

                result.IncomeApiStatus = incomeJson["status"]?.ToString();
                if (incomeJson["status"]?.ToString() == "200" && incomeJson["data"] != null)
                {
                    var dataArray = incomeJson["data"] as JArray;
                    result.IncomeDataCount = dataArray?.Count ?? 0;

                    if (dataArray != null && dataArray.Count > 0)
                    {
                        foreach (var item in dataArray)
                        {
                            string date = item["date"]?.ToString() ?? "";
                            string type = item["type"]?.ToString() ?? "";
                            string valueStr = item["value"]?.ToString() ?? "0";

                            if (date == result.CurrentQuarterDate && type == "IncomeAfterTaxes")
                            {
                                if (decimal.TryParse(valueStr, out decimal income))
                                {
                                    result.NetIncome = income;
                                    result.Step1_Success = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                // ==================== 取得當季股東權益 ====================
                result.Step2_Description = $"Step 2: 取得當季股東權益 ({currentQuarterLabel})";
                string currentEquityApiUrl = $"https://api.finmindtrade.com/api/v4/data?dataset=TaiwanStockBalanceSheet&data_id={stockId}&start_date={result.CurrentQuarterDate}&end_date={result.CurrentQuarterDate}&token={FINMIND_API_KEY}";
                result.CurrentEquityApiUrl = currentEquityApiUrl;

                var currentEquityResponse = await client.GetStringAsync(currentEquityApiUrl);
                var currentEquityJson = JObject.Parse(currentEquityResponse);

                result.CurrentEquityApiStatus = currentEquityJson["status"]?.ToString();
                if (currentEquityJson["status"]?.ToString() == "200" && currentEquityJson["data"] != null)
                {
                    var dataArray = currentEquityJson["data"] as JArray;
                    result.CurrentEquityDataCount = dataArray?.Count ?? 0;

                    if (dataArray != null && dataArray.Count > 0)
                    {
                        foreach (var item in dataArray)
                        {
                            string date = item["date"]?.ToString() ?? "";
                            string type = item["type"]?.ToString() ?? "";
                            string valueStr = item["value"]?.ToString() ?? "0";

                            if (date == result.CurrentQuarterDate && type == "Equity")
                            {
                                if (decimal.TryParse(valueStr, out decimal equity))
                                {
                                    result.CurrentEquity = equity;
                                    result.Step2_Success = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                // ==================== 取得上一季股東權益 ====================
                result.Step3_Description = $"Step 3: 取得上一季股東權益 ({previousQuarterLabel})";
                string previousEquityApiUrl = $"https://api.finmindtrade.com/api/v4/data?dataset=TaiwanStockBalanceSheet&data_id={stockId}&start_date={result.PreviousQuarterDate}&end_date={result.PreviousQuarterDate}&token={FINMIND_API_KEY}";
                result.PreviousEquityApiUrl = previousEquityApiUrl;

                var previousEquityResponse = await client.GetStringAsync(previousEquityApiUrl);
                var previousEquityJson = JObject.Parse(previousEquityResponse);

                result.PreviousEquityApiStatus = previousEquityJson["status"]?.ToString();
                if (previousEquityJson["status"]?.ToString() == "200" && previousEquityJson["data"] != null)
                {
                    var dataArray = previousEquityJson["data"] as JArray;
                    result.PreviousEquityDataCount = dataArray?.Count ?? 0;

                    if (dataArray != null && dataArray.Count > 0)
                    {
                        foreach (var item in dataArray)
                        {
                            string date = item["date"]?.ToString() ?? "";
                            string type = item["type"]?.ToString() ?? "";
                            string valueStr = item["value"]?.ToString() ?? "0";

                            if (date == result.PreviousQuarterDate && type == "Equity")
                            {
                                if (decimal.TryParse(valueStr, out decimal equity))
                                {
                                    result.PreviousEquity = equity;
                                    result.Step3_Success = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                // ==================== 計算 ROE ====================
                result.Step4_Description = "Step 4: 計算 ROE (使用近兩季股東權益平均)";
                if (result.Step1_Success && result.Step2_Success && result.Step3_Success)
                {
                    result.AverageEquity = (result.CurrentEquity + result.PreviousEquity) / 2;
                    result.Roe = (result.NetIncome / result.AverageEquity) * 100;
                    result.Step4_Success = true;
                    result.AllSuccess = true;
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.StackTrace = ex.StackTrace;
            }

            return result;
        }
    }

    public class SingleRoeTestResult
    {
        public string StockId { get; set; }
        public DateTime TestTime { get; set; }
        public string CurrentQuarterDate { get; set; }
        public string PreviousQuarterDate { get; set; }
        public string CurrentQuarterLabel { get; set; }
        public string PreviousQuarterLabel { get; set; }

        // Step 1: 稅後淨利
        public string Step1_Description { get; set; }
        public string IncomeApiUrl { get; set; }
        public string IncomeApiStatus { get; set; }
        public int IncomeDataCount { get; set; }
        public decimal NetIncome { get; set; }
        public bool Step1_Success { get; set; }

        // Step 2: 當季股東權益
        public string Step2_Description { get; set; }
        public string CurrentEquityApiUrl { get; set; }
        public string CurrentEquityApiStatus { get; set; }
        public int CurrentEquityDataCount { get; set; }
        public decimal CurrentEquity { get; set; }
        public bool Step2_Success { get; set; }

        // Step 3: 上一季股東權益
        public string Step3_Description { get; set; }
        public string PreviousEquityApiUrl { get; set; }
        public string PreviousEquityApiStatus { get; set; }
        public int PreviousEquityDataCount { get; set; }
        public decimal PreviousEquity { get; set; }
        public bool Step3_Success { get; set; }

        // Step 4: 計算 ROE
        public string Step4_Description { get; set; }
        public decimal AverageEquity { get; set; }
        public decimal Roe { get; set; }
        public bool Step4_Success { get; set; }

        // Result
        public bool AllSuccess { get; set; }
        public string Error { get; set; }
        public string StackTrace { get; set; }
    }
}
