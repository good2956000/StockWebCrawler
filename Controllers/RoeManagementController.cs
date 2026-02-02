using MvcWebCrawler.Data;
using MvcWebCrawler.Services;
using System;
using System.Web.Mvc;

namespace MvcWebCrawler.Controllers
{
    public class RoeManagementController : Controller
    {
        private readonly RoeRepository _roeRepository;

        public RoeManagementController()
        {
            _roeRepository = new RoeRepository();
        }

        // GET: RoeManagement
        public ActionResult Index()
        {
            var cacheStats = _roeRepository.GetCacheStats();
            
            ViewBag.CacheCount = cacheStats.Count;
            ViewBag.LastUpdate = cacheStats.LastUpdate;
            ViewBag.ServiceRunning = RoeServiceManager.IsRunning;
            ViewBag.NextExecutionTime = RoeServiceManager.NextExecutionTime;
            ViewBag.LastExecutionTime = RoeServiceManager.LastExecutionTime;

            return View();
        }

        // POST: 清除快取
        [HttpPost]
        public ActionResult ClearCache()
        {
            try
            {
                // 1. 先停止服務
                System.Diagnostics.Debug.WriteLine("[ROE Management] Stopping service before clearing cache...");
                RoeServiceManager.Stop();
                System.Threading.Thread.Sleep(1000); // 等待服務完全停止
                
                // 2. 清除快取
                System.Diagnostics.Debug.WriteLine("[ROE Management] Clearing cache...");
                _roeRepository.ClearAllCache();
                
                // 3. 重新啟動服務（這樣會重頭開始抓取）
                System.Diagnostics.Debug.WriteLine("[ROE Management] Restarting service...");
                System.Threading.Thread.Sleep(500);
                RoeServiceManager.Start();
                
                System.Diagnostics.Debug.WriteLine("[ROE Management] Cache cleared and service restarted successfully");
                TempData["Message"] = "快取已清除成功！服務已重啟，將重頭開始抓取資料。";
                TempData["MessageType"] = "success";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ROE Management] Error clearing cache: {ex.Message}");
                TempData["Message"] = $"清除快取失敗：{ex.Message}";
                TempData["MessageType"] = "danger";
            }

            return RedirectToAction("Index");
        }

        // POST: 重啟服務
        [HttpPost]
        public ActionResult RestartService()
        {
            try
            {
                RoeServiceManager.Stop();
                System.Threading.Thread.Sleep(1000);
                RoeServiceManager.Start();
                
                System.Diagnostics.Debug.WriteLine("[ROE Management] Service restarted successfully");
                TempData["Message"] = "服務已重啟成功！";
                TempData["MessageType"] = "success";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ROE Management] Error restarting service: {ex.Message}");
                TempData["Message"] = $"重啟服務失敗：{ex.Message}";
                TempData["MessageType"] = "danger";
            }

            return RedirectToAction("Index");
        }
    }
}
