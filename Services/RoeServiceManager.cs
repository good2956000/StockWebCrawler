using System;

namespace MvcWebCrawler.Services
{
    /// <summary>
    /// ROE 背景服務管理器
    /// </summary>
    public static class RoeServiceManager
    {
        private static RoeCollectionService _service;
        private static readonly object _lock = new object();
        private static DateTime? _lastExecutionTime;
        private static DateTime? _serviceStartTime;

        /// <summary>
        /// 啟動 ROE 蒐集服務
        /// </summary>
        public static void Start()
        {
            lock (_lock)
            {
                if (_service == null)
                {
                    _service = new RoeCollectionService();
                    _service.Start();
                    _serviceStartTime = DateTime.Now;
                    System.Diagnostics.Debug.WriteLine("[ROE Service Manager] Service started successfully");
                }
            }
        }

        /// <summary>
        /// 停止 ROE 蒐集服務
        /// </summary>
        public static void Stop()
        {
            lock (_lock)
            {
                if (_service != null)
                {
                    _service.Stop();
                    _service = null;
                    _serviceStartTime = null;
                    _lastExecutionTime = null;
                    System.Diagnostics.Debug.WriteLine("[ROE Service Manager] Service stopped successfully");
                }
            }
        }

        /// <summary>
        /// 取得服務運行狀態
        /// </summary>
        public static bool IsRunning
        {
            get { return _service != null; }
        }

        /// <summary>
        /// 取得最後執行時間
        /// </summary>
        public static DateTime? LastExecutionTime
        {
            get { return _lastExecutionTime; }
        }

        /// <summary>
        /// 取得服務啟動時間
        /// </summary>
        public static DateTime? ServiceStartTime
        {
            get { return _serviceStartTime; }
        }

        /// <summary>
        /// 更新最後執行時間
        /// </summary>
        public static void UpdateLastExecutionTime()
        {
            _lastExecutionTime = DateTime.Now;
        }

        /// <summary>
        /// 取得下次執行時間
        /// </summary>
        public static DateTime? NextExecutionTime
        {
            get
            {
                if (_lastExecutionTime.HasValue)
                {
                    return _lastExecutionTime.Value.AddHours(1);
                }
                else if (_serviceStartTime.HasValue)
                {
                    return _serviceStartTime.Value.AddHours(1);
                }
                return null;
            }
        }
    }
}
