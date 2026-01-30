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
    }
}
