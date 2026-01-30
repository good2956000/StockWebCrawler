using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace MvcWebCrawler.Data
{
    /// <summary>
    /// ROE 資料快取 Repository
    /// </summary>
    public class RoeRepository
    {
        private readonly string _filePath;
        private static readonly object _lockObject = new object();

        public RoeRepository()
        {
            try
            {
                // 資料存放在 App_Data 資料夾的 JSON 檔案
                string appDataPath = HttpContext.Current.Server.MapPath("~/App_Data");
                System.Diagnostics.Debug.WriteLine($"App_Data path: {appDataPath}");
                
                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                    System.Diagnostics.Debug.WriteLine($"Created App_Data directory");
                }
                
                _filePath = Path.Combine(appDataPath, "roe_cache.json");
                System.Diagnostics.Debug.WriteLine($"ROE cache file path: {_filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RoeRepository constructor: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 載入所有 ROE 快取資料
        /// </summary>
        private Dictionary<string, RoeData> LoadRoeCache()
        {
            lock (_lockObject)
            {
                try
                {
                    if (!File.Exists(_filePath))
                    {
                        System.Diagnostics.Debug.WriteLine("ROE cache file does not exist, returning empty dictionary");
                        return new Dictionary<string, RoeData>();
                    }

                    string json = File.ReadAllText(_filePath);
                    System.Diagnostics.Debug.WriteLine($"Loaded ROE cache JSON length: {json.Length}");
                    
                    var cache = JsonConvert.DeserializeObject<Dictionary<string, RoeData>>(json) ?? new Dictionary<string, RoeData>();
                    System.Diagnostics.Debug.WriteLine($"Loaded {cache.Count} ROE cache items");
                    
                    return cache;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading ROE cache: {ex.Message}");
                    return new Dictionary<string, RoeData>();
                }
            }
        }

        /// <summary>
        /// 儲存所有 ROE 快取資料
        /// </summary>
        private void SaveRoeCache(Dictionary<string, RoeData> cache)
        {
            lock (_lockObject)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(cache, Formatting.Indented);
                    File.WriteAllText(_filePath, json);
                    System.Diagnostics.Debug.WriteLine($"Saved {cache.Count} ROE cache items to {_filePath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving ROE cache: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// 取得指定股票的 ROE 資料（從快取）
        /// </summary>
        public RoeData GetRoe(string stockId)
        {
            try
            {
                var cache = LoadRoeCache();
                
                if (cache.ContainsKey(stockId))
                {
                    var roeData = cache[stockId];
                    System.Diagnostics.Debug.WriteLine($"Found ROE cache for {stockId}: Q2 2025 = {roeData.Q2_2025}%");
                    return roeData;
                }
                
                System.Diagnostics.Debug.WriteLine($"No ROE cache found for {stockId}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetRoe: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 儲存指定股票的 ROE 資料
        /// </summary>
        public bool SaveRoe(string stockId, RoeData roeData)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Saving ROE for {stockId}: Q2 2025 = {roeData.Q2_2025}%");
                
                var cache = LoadRoeCache();
                cache[stockId] = roeData;
                SaveRoeCache(cache);
                
                System.Diagnostics.Debug.WriteLine($"Successfully saved ROE for {stockId}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SaveRoe: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 檢查指定股票是否有 ROE 快取
        /// </summary>
        public bool HasRoeCache(string stockId)
        {
            try
            {
                var cache = LoadRoeCache();
                bool hasCache = cache.ContainsKey(stockId);
                System.Diagnostics.Debug.WriteLine($"HasRoeCache({stockId}): {hasCache}");
                return hasCache;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in HasRoeCache: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 清除所有 ROE 快取並重新建立空檔案
        /// </summary>
        public bool ClearAllCache()
        {
            try
            {
                lock (_lockObject)
                {
                    // 建立空的快取字典
                    var emptyCache = new Dictionary<string, RoeData>();
                    
                    // 儲存空的 JSON 檔案
                    SaveRoeCache(emptyCache);
                    
                    System.Diagnostics.Debug.WriteLine("Cleared all ROE cache and created empty cache file");
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ClearAllCache: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 取得快取統計資訊
        /// </summary>
        public (int Count, DateTime? LastUpdate) GetCacheStats()
        {
            try
            {
                var cache = LoadRoeCache();
                int count = cache.Count;
                DateTime? lastUpdate = null;

                if (cache.Count > 0)
                {
                    lastUpdate = cache.Values.Max(x => x.CachedDate);
                }

                return (count, lastUpdate);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetCacheStats: {ex.Message}");
                return (0, null);
            }
        }
    }

    /// <summary>
    /// ROE 資料模型（支援 4 個季度）
    /// </summary>
    public class RoeData
    {
        /// <summary>
        /// 股票代號
        /// </summary>
        public string StockId { get; set; }

        /// <summary>
        /// 第 1 季 ROE (%)
        /// </summary>
        public string Q1_ROE { get; set; }

        /// <summary>
        /// 第 2 季 ROE (%)
        /// </summary>
        public string Q2_ROE { get; set; }

        /// <summary>
        /// 第 3 季 ROE (%)
        /// </summary>
        public string Q3_ROE { get; set; }

        /// <summary>
        /// 第 4 季 ROE (%)
        /// </summary>
        public string Q4_ROE { get; set; }

        /// <summary>
        /// 第 1 季日期
        /// </summary>
        public string Q1_Date { get; set; }

        /// <summary>
        /// 第 2 季日期
        /// </summary>
        public string Q2_Date { get; set; }

        /// <summary>
        /// 第 3 季日期
        /// </summary>
        public string Q3_Date { get; set; }

        /// <summary>
        /// 第 4 季日期
        /// </summary>
        public string Q4_Date { get; set; }

        /// <summary>
        /// 快取日期
        /// </summary>
        public DateTime CachedDate { get; set; }

        /// <summary>
        /// 本期淨利 Q1（用於計算）
        /// </summary>
        public decimal NetIncome_Q1 { get; set; }

        /// <summary>
        /// 股東權益 Q1（用於計算）
        /// </summary>
        public decimal Equity_Q1 { get; set; }

        /// <summary>
        /// 本期淨利 Q2（用於計算）
        /// </summary>
        public decimal NetIncome_Q2 { get; set; }

        /// <summary>
        /// 股東權益 Q2（用於計算）
        /// </summary>
        public decimal Equity_Q2 { get; set; }

        /// <summary>
        /// 本期淨利 Q3（用於計算）
        /// </summary>
        public decimal NetIncome_Q3 { get; set; }

        /// <summary>
        /// 股東權益 Q3（用於計算）
        /// </summary>
        public decimal Equity_Q3 { get; set; }

        /// <summary>
        /// 本期淨利 Q4（用於計算）
        /// </summary>
        public decimal NetIncome_Q4 { get; set; }

        /// <summary>
        /// 股東權益 Q4（用於計算）
        /// </summary>
        public decimal Equity_Q4 { get; set; }

        // 保留舊的欄位名稱以向下相容
        [JsonIgnore]
        public string Q2_2025
        {
            get => Q1_ROE;
            set => Q1_ROE = value;
        }

        [JsonIgnore]
        public string Q3_2025
        {
            get => Q2_ROE;
            set => Q2_ROE = value;
        }

        [JsonIgnore]
        public string Q4_2025
        {
            get => Q3_ROE;
            set => Q3_ROE = value;
        }

        public RoeData()
        {
            Q1_ROE = "N/A";
            Q2_ROE = "N/A";
            Q3_ROE = "N/A";
            Q4_ROE = "N/A";
            Q1_Date = "";
            Q2_Date = "";
            Q3_Date = "";
            Q4_Date = "";
            CachedDate = DateTime.Now;
        }
    }
}
