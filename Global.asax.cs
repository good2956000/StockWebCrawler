using MvcWebCrawler.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace StockWebCrawler
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // 啟動 ROE 背景蒐集服務
            RoeServiceManager.Start();
            System.Diagnostics.Debug.WriteLine("[Application] ROE Collection Service started");
        }

        protected void Application_End()
        {
            // 停止 ROE 背景蒐集服務
            RoeServiceManager.Stop();
            System.Diagnostics.Debug.WriteLine("[Application] ROE Collection Service stopped");
        }
    }
}
