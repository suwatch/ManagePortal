﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Routing;

namespace ARMExplorer.App_Start
{
    public class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.Routes.MapHttpRoute("get-operations", "api/operations", new { controller = "Operation", action = "Get" }, new { verb = new HttpMethodConstraint("GET", "HEAD") });
            config.Routes.MapHttpRoute("invoke-operation", "api/operations", new { controller = "Operation", action = "Invoke" }, new { verb = new HttpMethodConstraint("POST") });

            config.Routes.MapHttpRoute("get-token", "api/token", new { controller = "ARM", action = "GetToken" }, new { verb = new HttpMethodConstraint("GET", "HEAD") });
            config.Routes.MapHttpRoute("get", "api/{*path}", new { controller = "ARM", action = "Get" }, new { verb = new HttpMethodConstraint("GET", "HEAD") });

            GlobalConfiguration.Configuration.Formatters.XmlFormatter.SupportedMediaTypes.Clear();
        }
    }
}