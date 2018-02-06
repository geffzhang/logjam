﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="BaseOwinTest.cs">
// Copyright (c) 2011-2018 https://github.com/logjam2.  
// </copyright>
// Licensed under the <a href="https://github.com/logjam2/logjam/blob/master/LICENSE.txt">Apache License, Version 2.0</a>;
// you may not use this file except in compliance with the License.
// --------------------------------------------------------------------------------------------------------------------


using System;
using System.IO;
using System.Net;

using LogJam.Config;
using LogJam.Owin.Http;
using LogJam.Shared.Internal;
using LogJam.Trace;
using LogJam.Trace.Config;
using LogJam.Trace.Format;
using LogJam.Trace.Switches;

using Microsoft.Owin;
using Microsoft.Owin.Testing;

using Owin;

using Xunit.Abstractions;

namespace LogJam.Owin.UnitTests
{

    /// <summary>
    /// Common OWIN test logic.
    /// </summary>
    public abstract class BaseOwinTest
    {

        protected readonly PathString ExceptionPath = new PathString("/exception");
        protected readonly PathString TracePath = new PathString("/trace");
        protected readonly PathString LogJamStatusPath = new PathString("/logjam/status");

        protected readonly ITestOutputHelper testOutputHelper;

        protected BaseOwinTest(ITestOutputHelper testOutputHelper)
        {
            Arg.NotNull(testOutputHelper, nameof(testOutputHelper));

            this.testOutputHelper = testOutputHelper;
        }

        public TestServer CreateTestServer(TextWriter logTarget, SetupLog setupLog, bool backgroundThreadLogging = true)
        {
            Arg.NotNull(logTarget, nameof(logTarget));

            string appName = GetType().FullName;

            return TestServer.Create(appBuilder =>
                                     {
                                         appBuilder.Properties["host.AppName"] = appName;

                                         // Use the specified setupLog
                                         appBuilder.RegisterLogManager(new TraceManager(setupLog));

                                         // Configure logging
                                         ConfigureLogging(appBuilder, logTarget, backgroundThreadLogging);

                                         // Run the ExceptionLoggingMiddleware _after_ the Owin.Diagnostics.Error page, which will result in exceptions being 
                                         // logged twice, and results in the Owin.Diagnostis.Error page converting the Exception into a response
                                         appBuilder.UseErrorPage();
                                         appBuilder.TraceExceptions(logFirstChance: false, logUnhandled: true);

                                         // Request handling
                                         appBuilder.Use(async (owinContext, next) =>
                                                        {
                                                            IOwinRequest req = owinContext.Request;
                                                            IOwinResponse res = owinContext.Response;

                                                            if (req.Path == ExceptionPath)
                                                            {
                                                                throw new Exception("Expected exception thrown from /exception URL");
                                                            }
                                                            else if (req.Path == TracePath)
                                                            {
                                                                string requestUri = req.Uri.ToString();
                                                                string strTraceCount = req.Query["traceCount"];
                                                                int traceCount = 3;
                                                                int.TryParse(strTraceCount, out traceCount);

                                                                var tracer = owinContext.GetTracerFactory().TracerFor(this);
                                                                for (int i = 0; i < traceCount; ++i)
                                                                {
                                                                    tracer.Verbose("{0} #{1}", requestUri, i);
                                                                }
                                                            }
                                                            else if (req.Path == LogJamStatusPath)
                                                            {
                                                                res.StatusCode = (int) HttpStatusCode.OK;
                                                                res.ContentType = "text/plain";
                                                                var sw = new StringWriter();
                                                                LogManager logManager = owinContext.GetLogManager();
                                                                logManager.SetupLog.WriteEntriesTo(sw);
                                                                res.Write(sw.ToString());
                                                            }
                                                            else
                                                            {
                                                                await next();
                                                            }
                                                        });

                                         logTarget.WriteLine("TestAuthServer configuration complete.");
                                     });
        }

        /// <summary>
        /// Logging configuration - overrideable
        /// </summary>
        /// <param name="appBuilder"></param>
        /// <param name="logTarget"></param>
        /// <param name="backgroundThreadLogging"></param>
        protected virtual void ConfigureLogging(IAppBuilder appBuilder, TextWriter logTarget, bool backgroundThreadLogging)
        {
            var textLogConfig = appBuilder.GetLogManagerConfig().UseTextWriter(logTarget)
                                          .Format(new DefaultTraceFormatter())
                                          .Format(new HttpRequestFormatter())
                                          .Format(new HttpResponseFormatter());
            textLogConfig.BackgroundLogging = backgroundThreadLogging;
            appBuilder.GetTraceManagerConfig().TraceTo(textLogConfig, Tracer.All, new OnOffTraceSwitch(true));
            appBuilder.LogHttpRequests(textLogConfig);
            appBuilder.UseOwinTracerLogging();
        }

        protected void IssueTraceRequest(TestServer testServer, int traceCount)
        {
            var task = testServer.CreateRequest("/trace?traceCount=" + traceCount).GetAsync();
            var response = task.Result; // Wait for the call to complete
        }

    }

}
