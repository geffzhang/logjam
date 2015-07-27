﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AppBuilderExtensions.cs">
// Copyright (c) 2011-2014 logjam.codeplex.com.  
// </copyright>
// Licensed under the <a href="http://logjam.codeplex.com/license">Apache License, Version 2.0</a>;
// you may not use this file except in compliance with the License.
// --------------------------------------------------------------------------------------------------------------------

using System.Linq;

namespace Owin
{
	using System.Threading;

	using LogJam;
	using LogJam.Config;
	using LogJam.Owin;
	using LogJam.Owin.Http;
	using LogJam.Trace;
	using LogJam.Trace.Config;
	using LogJam.Trace.Switches;
	using LogJam.Writer;

	using Microsoft.Owin;
	using Microsoft.Owin.BuilderProperties;
	using Microsoft.Owin.Logging;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.Contracts;
	using System.IO;


	/// <summary>
	/// AppBuilder extension methods for diagnostics.
	/// </summary>
	public static class AppBuilderExtensions
	{

		private const string c_logManagerKey = OwinContextExtensions.LogManagerKey;
		private const string c_tracerFactoryKey = OwinContextExtensions.TracerFactoryKey;

		private const string c_logManagerConfigKey = "LogJam.Config.LogManagerConfig";
		private const string c_traceManagerConfigKey = "LogJam.Trace.Config.TraceManagerConfig";

		/// <summary>
		/// Returns the <see cref="GetLogManagerConfig"/> used to configure LogJam logging.
		/// </summary>
		/// <param name="appBuilder"></param>
		/// <returns></returns>
		public static LogManagerConfig GetLogManagerConfig(this IAppBuilder appBuilder)
		{
			Contract.Requires<ArgumentNullException>(appBuilder != null);

			var config = appBuilder.Properties.Get<LogManagerConfig>(c_logManagerConfigKey);
			if (config == null)
			{
				// Includes creating the LogManagerConfig
				appBuilder.RegisterLogManager();
				config = appBuilder.Properties.Get<LogManagerConfig>(c_logManagerConfigKey);
			}

			return config;
		}

		/// <summary>
		/// Returns the <see cref="GetTraceManagerConfig"/> used to configure LogJam tracing.
		/// </summary>
		/// <param name="appBuilder"></param>
		/// <returns></returns>
		public static TraceManagerConfig GetTraceManagerConfig(this IAppBuilder appBuilder)
		{
			Contract.Requires<ArgumentNullException>(appBuilder != null);

			var config = appBuilder.Properties.Get<TraceManagerConfig>(c_traceManagerConfigKey);
			if (config == null)
			{
				// Includes creating the TraceManagerConfig
				appBuilder.RegisterLogManager();
				config = appBuilder.Properties.Get<TraceManagerConfig>(c_traceManagerConfigKey);
			}

			return config;
		}

		/// <summary>
		/// Retrieves the <see cref="ITracerFactory"/> from the Properties collection.
		/// </summary>
		/// <param name="appBuilder"></param>
		/// <returns></returns>
		public static ITracerFactory GetTracerFactory(this IAppBuilder appBuilder)
		{
			Contract.Requires<ArgumentNullException>(appBuilder != null);

			ITracerFactory tracerFactory = appBuilder.Properties.Get<ITracerFactory>(c_tracerFactoryKey);
			if (tracerFactory == null)
			{
				// Includes creating the TraceManager
				appBuilder.RegisterLogManager();
				tracerFactory = appBuilder.Properties.Get<ITracerFactory>(c_tracerFactoryKey);
			}

			return tracerFactory;
		}

		/// <summary>
		/// Retrieves the <see cref="ITracerFactory"/> from the Properties collection.
		/// </summary>
		/// <param name="appBuilder"></param>
		/// <returns></returns>
		public static LogManager GetLogManager(this IAppBuilder appBuilder)
		{
			Contract.Requires<ArgumentNullException>(appBuilder != null);

			LogManager logManager = appBuilder.Properties.Get<LogManager>(c_logManagerKey);
			if (logManager == null)
			{
				// Includes creating the LogManager
				logManager = appBuilder.RegisterLogManager();
			}

			return logManager;
		}

		/// <summary>
		/// Logs to the OWIN <c>host.TraceOutput</c> stream.  It seems slow, so developer beware...
		/// </summary>
		/// <param name="appBuilder"></param>
		/// <returns></returns>
		public static TextWriterLogWriterConfig LogToOwinTraceOutput(this IAppBuilder appBuilder)
		{
			Contract.Requires<ArgumentNullException>(appBuilder != null);

			var owintraceOutputTextWriter = appBuilder.Properties.Get<TextWriter>("host.TraceOutput");
			if (owintraceOutputTextWriter == null)
			{
				return null;
			}

			var logWriterConfig = appBuilder.LogToText(() => owintraceOutputTextWriter);
			logWriterConfig.DisposeTextWriter = false;
			return logWriterConfig;
		}

		/// <summary>
		/// Logs to the <see cref="TextWriter"/> returned from <paramref name="textWriterCreateFunc"/>.
		/// </summary>
		/// <param name="appBuilder"></param>
		/// <param name="textWriterCreateFunc">A function that is called once each time logging starts, to obtain a <see cref="TextWriter"/>.</param>
		/// <returns></returns>
		public static TextWriterLogWriterConfig LogToText(this IAppBuilder appBuilder, Func<TextWriter> textWriterCreateFunc)
		{
			Contract.Requires<ArgumentNullException>(appBuilder != null);
			Contract.Requires<ArgumentNullException>(textWriterCreateFunc != null);

			return appBuilder.GetLogManagerConfig().UseTextWriter(textWriterCreateFunc);
		}

        /// <summary>
        /// Registers <see cref="LogManager"/> and <see cref="TraceManager"/> instances, along with <see cref="LogManagerConfig"/> and <see cref="TraceManagerConfig"/>, with the <paramref name="appBuilder"/>.  In addition,
        /// this method registers OWIN middleware that associates the application <see cref="LogManager"/> and <see cref="TraceManager"/> with each request.
        /// </summary>
        /// <param name="appBuilder"></param>
        /// <param name="logManager"></param>
        /// <param name="traceManager"></param>
        /// <remarks>LogManager and TraceManager registration is run automatically by several other configuration methods, so usually does not need to
        /// be explicitly called.  If it IS explicitly called (for example to use a custom <see cref="LogManager"/> for testing), it should be 
        /// called before any other <c>LogJam.Owin</c> configuration methods.</remarks>
        public static LogManager RegisterLogManager(this IAppBuilder appBuilder, TraceManager traceManager, LogManager logManager = null)
        {
            Contract.Requires<ArgumentNullException>(appBuilder != null);
            Contract.Requires<ArgumentNullException>(traceManager != null);

            if (logManager == null)
            {
                logManager = traceManager.LogManager;
            }
            else if (logManager != traceManager.LogManager)
            {
                throw new LogJamOwinSetupException("Not supported to register disassociated TraceManager and LogManager instances.", appBuilder);
            }

            appBuilder.Properties[c_logManagerConfigKey] = logManager.Config;
            appBuilder.Properties[c_traceManagerConfigKey] = traceManager.Config;

            appBuilder.Properties[c_logManagerKey] = logManager;
            appBuilder.Properties[c_tracerFactoryKey] = traceManager;

            // LogJamManagerMiddleware manages lifetime + registering the LogManager and TraceManager per request
            appBuilder.Use<LogJamManagerMiddleware>(logManager, traceManager);

            // Ensure LogManager.Dispose - LogJamManagerMiddleware.Dispose() is not reliably called.
            var properties = new AppProperties(appBuilder.Properties);
            properties.OnAppDisposing.Register(() =>
            {
                traceManager.Dispose();
                logManager.Dispose();
            });

            return logManager;
        }

		/// <summary>
		/// Registers <see cref="LogManager"/> and <see cref="TraceManager"/> instances, along with <see cref="LogManagerConfig"/> and <see cref="TraceManagerConfig"/>, with the <paramref name="appBuilder"/>.  In addition,
		/// this method registers OWIN middleware that associates the application <see cref="LogManager"/> and <see cref="TraceManager"/> with each request.
		/// </summary>
		/// <param name="appBuilder"></param>
		/// <param name="setupTracerFactory">An optional <see cref="ITracerFactory"/> used to trace logjam setup operations - <see cref="SetupLog"/>.</param>
		/// <param name="logManagerConfig"></param>
		/// <param name="traceManagerConfig"></param>
		/// <remarks>This registration is run automatically by several other configuration methods, so usually does not need to
		/// be explicitly called.  If it IS explicitly called (for example to use a custom <paramref name="setupTracerFactory"/>), it should be 
		/// called before any other <c>LogJam.Owin</c> configuration methods.</remarks>
		public static LogManager RegisterLogManager(this IAppBuilder appBuilder, ITracerFactory setupTracerFactory = null, LogManagerConfig logManagerConfig = null, TraceManagerConfig traceManagerConfig = null)
		{
			Contract.Requires<ArgumentNullException>(appBuilder != null);

			var logManager = appBuilder.Properties.Get<LogManager>(c_logManagerKey);
			if (logManager == null)
			{
				var tracerFactory = appBuilder.Properties.Get<ITracerFactory>(c_tracerFactoryKey);
				if (tracerFactory != null)
				{
					throw new LogJamOwinSetupException("Not supported to register the ITracerFactory before the LogManager.", appBuilder);
				}

				logManagerConfig = logManagerConfig ?? new LogManagerConfig();
				traceManagerConfig = traceManagerConfig ?? new TraceManagerConfig();
				appBuilder.Properties.Add(c_logManagerConfigKey, logManagerConfig);
				appBuilder.Properties.Add(c_traceManagerConfigKey, traceManagerConfig);


				logManager = new LogManager(logManagerConfig, setupTracerFactory);
				var traceManager = new TraceManager(logManager, traceManagerConfig);
				appBuilder.Properties.Add(c_logManagerKey, logManager);
				appBuilder.Properties.Add(c_tracerFactoryKey, traceManager);

				// LogJamManagerMiddleware manages lifetime + registering the LogManager and TraceManager per request
				appBuilder.Use<LogJamManagerMiddleware>(logManager, traceManager);

				// Ensure LogManager.Dispose - LogJamManagerMiddleware.Dispose() is not reliably called.
				var properties = new AppProperties(appBuilder.Properties);
				properties.OnAppDisposing.Register(() =>
				                                   {
					                                   traceManager.Dispose();
					                                   logManager.Dispose();
				                                   });
			}

			return logManager;
		}

		/// <summary>
		/// Enables logging of all HTTP requests to the specified <paramref name="configuredLogWriters"/>.
		/// </summary>
		/// <param name="appBuilder"></param>
		/// <param name="configuredLogWriters">Specifies the log writers to use for HTTP logging.</param>
		/// <remarks>This middleware should be enabled at or near the beginning of the OWIN pipeline.</remarks>
		public static void LogHttpRequests(this IAppBuilder appBuilder, params ILogWriterConfig[] configuredLogWriters)
		{
			Contract.Requires<ArgumentNullException>(appBuilder != null);
            Contract.Requires<ArgumentNullException>(configuredLogWriters != null);

			var logManager = appBuilder.GetLogManager();
			var tracerFactory = appBuilder.GetTracerFactory();

			if (configuredLogWriters.Any())
			{
				appBuilder.Use<HttpLoggingMiddleware>(logManager, tracerFactory, configuredLogWriters);
			}
		}

        /// <summary>
        /// Enables logging of all HTTP requests to the specified <paramref name="configuredLogWriters"/>.
        /// </summary>
        /// <param name="appBuilder"></param>
        /// <param name="configuredLogWriters">Specifies the log writers to use for HTTP logging.</param>
        /// <remarks>This middleware should be enabled at or near the beginning of the OWIN pipeline.</remarks>
        public static void LogHttpRequests(this IAppBuilder appBuilder, IEnumerable<ILogWriterConfig> configuredLogWriters)
        {
            Contract.Requires<ArgumentNullException>(appBuilder != null);
            Contract.Requires<ArgumentNullException>(configuredLogWriters != null);

            LogHttpRequests(appBuilder, configuredLogWriters.ToArray());
        }

        /// <summary>
		/// Enables sending trace messages to <paramref name="configuredLogWriters"/>.  This method can be called multiple times to
		/// specify different switch settings for different logWriters; or <see cref="GetTraceManagerConfig"/> can
		/// be used for finer-grained control of configuration.
		/// </summary>
		/// <param name="appBuilder"></param>
		/// <param name="switches"></param>
		/// <param name="configuredLogWriters"></param>
		/// <returns></returns>
		public static void TraceTo(this IAppBuilder appBuilder, SwitchSet switches, params ILogWriterConfig[] configuredLogWriters)
		{
			Contract.Requires<ArgumentNullException>(appBuilder != null);
			Contract.Requires<ArgumentNullException>(switches != null);

			foreach (var logWriterConfig in configuredLogWriters)
			{
				appBuilder.GetTraceManagerConfig().Writers.Add(new TraceWriterConfig(logWriterConfig, switches));
			}
		}

		/// <summary>
		/// Enables sending trace messages to <paramref name="configuredLogWriters"/>.  This overload uses default trace threshold
		/// (<see cref="TraceLevel.Info"/>) for all tracing.
		/// </summary>
		/// <param name="appBuilder"></param>
		/// <param name="configuredLogWriters"></param>
		/// <returns></returns>
		public static void TraceTo(this IAppBuilder appBuilder, params ILogWriterConfig[] configuredLogWriters)
		{
			Contract.Requires<ArgumentNullException>(appBuilder != null);

			var traceSwitch = new ThresholdTraceSwitch(TraceLevel.Info);
			var switches = new SwitchSet()
			               {
				               { Tracer.All, traceSwitch }
			               };
			appBuilder.TraceTo(switches, configuredLogWriters);
		}

		/// <summary>
		/// Returns a <see cref="Tracer"/> for type <typeparamref name="T"/>.
		/// </summary>
		/// <param name="appBuilder"></param>
		/// <returns></returns>
		public static Tracer TracerFor<T>(this IAppBuilder appBuilder)
		{
			Contract.Requires<ArgumentNullException>(appBuilder != null);

			return appBuilder.GetTracerFactory().TracerFor<T>();
		}

		/// <summary>
		/// Uses LogJam <see cref="Tracer"/> logging for all OWIN logging.
		/// </summary>
		/// <param name="appBuilder"></param>
		/// <returns></returns>
		public static IAppBuilder UseOwinTracerLogging(this IAppBuilder appBuilder)
		{
			Contract.Requires<ArgumentNullException>(appBuilder != null);

			appBuilder.SetLoggerFactory(new OwinLoggerFactory(appBuilder.GetTracerFactory()));

			return appBuilder;
		}

		/// <summary>
		/// Turns on tracing of first-chance and/or unhandled OWIN exceptions.
		/// </summary>
		/// <param name="appBuilder"></param>
		/// <param name="logFirstChance"><c>true</c> to log first-chance exceptions - logs every exception that is thrown.</param>
		/// <param name="logUnhandled"><c>true</c> to log unhandled exceptions in the Owin pipeline.</param>
		/// <returns></returns>
		public static IAppBuilder TraceExceptions(this IAppBuilder appBuilder, bool logFirstChance = false, bool logUnhandled = true)
		{
			Contract.Requires<ArgumentNullException>(appBuilder != null);
			if (logFirstChance || logUnhandled)
			{
				var tracer = appBuilder.TracerFor<ExceptionLoggingMiddleware>();

				appBuilder.Use<ExceptionLoggingMiddleware>(tracer, null, logFirstChance, logUnhandled);
			}

			return appBuilder;
		}

	}

}
