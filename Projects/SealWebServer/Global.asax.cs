﻿using System.Diagnostics;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;
using Seal.Helpers;
using Seal.Model;
using SealWebServer.Controllers;
using System.Configuration;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SealWebServer
{
    public class MvcApplication : System.Web.HttpApplication
    {
        public static bool DebugMode = false;

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();

            WebApiConfig.Register(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            WebHelper.WriteLogEntryWeb(EventLogEntryType.Information, "Starting Web Report Server");
            Audit.LogEventAudit(AuditType.EventServer, "Starting Web Report Server");
            Audit.LogEventAudit(AuditType.EventLoggedUsers, "0");

            DebugMode = (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["DebugMode"]) && ConfigurationManager.AppSettings["DebugMode"].ToLower() == "true");

            var preload = ConfigurationManager.AppSettings["PreLoad"];
            if (preload == null || preload.ToLower() == "true")
            {
                //Preload templates and dashboard widgets
                Task.Run(() => PreLoadThread());
            }

            var runScheduler = ConfigurationManager.AppSettings["RunScheduler"];
            if (runScheduler != null && runScheduler.ToLower() == "true" && Repository.Instance.Configuration.UseSealScheduler)
            {
                WebHelper.WriteLogEntryWeb(EventLogEntryType.Information, "Starting Scheduler from the Web Report Server");
                //Run scheduler
                Task.Run(() => RunScheduler());
            }
        }

        private void RunScheduler()
        {
            try
            {
                //Wait for application path to be set
                while (string.IsNullOrEmpty(Repository.Instance.WebApplicationPath)) Thread.Sleep(1000);

                SealReportScheduler.Instance.Run();
            }
            catch (Exception ex)
            {
                WebHelper.WriteLogEntryWeb(EventLogEntryType.Error, ex.Message);
            }
        }


        private void PreLoadThread()
        {
            try
            {
                WebHelper.WriteLogEntryWeb(EventLogEntryType.Information, "Starting Preload Templates");
                RepositoryServer.PreLoadTemplates();

                WebHelper.WriteLogEntryWeb(EventLogEntryType.Information, "Starting Preload Widgets");
                var widgets = DashboardWidgetsPool.Widgets;

                List<string> reportList = new List<string>();
                foreach (var widget in widgets.Values)
                {
                    var filePath = Repository.Instance.ReportsFolder + widget.ReportPath;
                    if (System.IO.File.Exists(filePath) && !reportList.Contains(filePath)) reportList.Add(filePath);
                }

                WebHelper.WriteLogEntryWeb(EventLogEntryType.Information, "Starting Preload of {0} Widget Reports", reportList.Count);
                var repository = Repository.Instance.CreateFast();
                foreach (var reportPath in reportList)
                {
                    try {
                        var report = Report.LoadFromFile(reportPath, repository);

                        report.ExecutionContext = ReportExecutionContext.TaskScheduler;
                        //Disable basics
                        report.ExecutionView.InitParameters(false);
                        report.ExecutionView.SetParameter(Parameter.DrillEnabledParameter, false);
                        report.ExecutionView.SetParameter(Parameter.SubReportsEnabledParameter, false);
                        report.ExecutionView.SetParameter(Parameter.ServerPaginationParameter, false);
                        //set HTML Format
                        report.ExecutionView.SetParameter(Parameter.ReportFormatParameter, ReportFormat.html.ToString());
                        //Force load of all models
                        report.ExecutionView.SetParameter(Parameter.ForceModelsLoad, true);

                        var execution = new ReportExecution() { Report = report };
                        execution.Execute();
                        while (report.IsExecuting) Thread.Sleep(100);
                    }
                    catch (Exception ex)
                    {
                        WebHelper.WriteLogEntryWeb(EventLogEntryType.Error, "Pre Load: Error executing '{0}\r\n{1}", reportPath, ex.Message);
                    }
                }

                WebHelper.WriteLogEntryWeb(EventLogEntryType.Information, "Ending Preload");
            }
            catch (Exception ex)
            {
                WebHelper.WriteLogEntryWeb(EventLogEntryType.Error, ex.Message);
            }
        }

        protected void Application_End()
        {
            SealReportScheduler.Instance.Shutdown();
            WebHelper.WriteLogEntryWeb(EventLogEntryType.Information, "Ending Web Report Server");
            Audit.LogEventAudit(AuditType.EventServer, "Ending Web Report Server");
            Audit.LogEventAudit(AuditType.EventLoggedUsers, "0");
        }

        protected void Session_Start()
        {
        }

        protected void Session_End()
        {
            SecurityUser user = null;
            if (Session[HomeController.SessionUser] != null)
            {
                user = (SecurityUser)Session[HomeController.SessionUser];
                WebHelper.WriteLogEntryWeb(EventLogEntryType.Information, "Ending Web Session '{0}' for user '{1}'", Session.SessionID, user.Name);
                user.Logout();
            }
            else
            {
                WebHelper.WriteLogEntryWeb(EventLogEntryType.Information, "Ending Web Session '{0}'", Session.SessionID);
            }
        }
    }
}