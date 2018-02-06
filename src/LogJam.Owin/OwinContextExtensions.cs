﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="OwinContextExtensions.cs">
// Copyright (c) 2011-2018 https://github.com/logjam2.  
// </copyright>
// Licensed under the <a href="https://github.com/logjam2/logjam/blob/master/LICENSE.txt">Apache License, Version 2.0</a>;
// you may not use this file except in compliance with the License.
// --------------------------------------------------------------------------------------------------------------------


using System;
using System.Collections.Generic;

using LogJam;
using LogJam.Owin;
using LogJam.Owin.Http;
using LogJam.Shared.Internal;
using LogJam.Trace;
#if CODECONTRACTS
using System.Diagnostics.Contracts;
#endif

namespace Microsoft.Owin
{

    /// <summary>
    /// Extension methods for <see cref="IOwinContext" />.
    /// </summary>
    public static class OwinContextExtensions
    {

        private const string c_lastLoggedExceptionKey = "LogJam.Owin.LastLoggedException";

        internal const string TracerFactoryKey = "LogJam.TracerFactory";
        internal const string LogManagerKey = "LogJam.LogManager";

        /// <summary>
        /// Returns the request number (ordinal) for the request described by <paramref name="owinContext" />. For each
        /// Owin app, this number starts at 1 upon initialization.
        /// </summary>
        /// <param name="owinContext">An <see cref="IOwinContext" /> for the current request.</param>
        /// <returns>The request number for the current OWIN request.</returns>
        public static long GetRequestNumber(this IOwinContext owinContext)
        {
            Arg.NotNull(owinContext, nameof(owinContext));

            return owinContext.Get<long>(HttpLoggingMiddleware.RequestNumberKey);
        }

        /// <summary>
        /// Stores <paramref name="exception" /> in the owin context dictionary, to support preventing duplicate
        /// logging of the exception.
        /// </summary>
        /// <param name="owinContext">An <see cref="IOwinContext" /> for the current request.</param>
        /// <param name="exception">An exception to store. If <c>null</c>, the stored exception is cleared.</param>
        public static void LoggedRequestException(this IOwinContext owinContext, Exception exception)
        {
            Arg.NotNull(owinContext, nameof(owinContext));

            owinContext.Environment[c_lastLoggedExceptionKey] = exception;
        }

        /// <summary>
        /// Returns <c>true</c> if <see cref="LoggedRequestException" /> was previously called for the same request,
        /// and same exception. Reference comparison is used to match the exception. This method is used to
        /// prevent duplicate logging of the exception.
        /// </summary>
        /// <param name="owinContext">An <see cref="IOwinContext" /> for the current request.</param>
        /// <param name="exception">An exception to compare to a stored exception. May not be <c>null</c>.</param>
        /// <returns><c>true</c> if <paramref name="exception" /> has already been logged for this request.</returns>
        public static bool HasRequestExceptionBeenLogged(this IOwinContext owinContext, Exception exception)
        {
            Arg.NotNull(owinContext, nameof(owinContext));
            Arg.NotNull(exception, nameof(exception));

            var lastLoggedException = owinContext.Get<Exception>(c_lastLoggedExceptionKey);
            return ReferenceEquals(exception, lastLoggedException);
        }

        /// <summary>
        /// Retrieves the <see cref="ITracerFactory" /> from the <paramref name="owinContext" />.
        /// </summary>
        /// <param name="owinContext">An <see cref="IOwinContext" /> for the current request.</param>
        /// <returns></returns>
        public static ITracerFactory GetTracerFactory(this IOwinContext owinContext)
        {
            Arg.NotNull(owinContext, nameof(owinContext));
#if CODECONTRACTS
            Contract.Ensures(Contract.Result<ITracerFactory>() != null);
#endif

            ITracerFactory tracerFactory = owinContext.Environment.Get<ITracerFactory>(TracerFactoryKey);
            if (tracerFactory != null)
            {
                return tracerFactory;
            }

            // If not set (eg AppBuilderExtensions.SetTracerFactory() wasn't called), return the global instance
            return TraceManager.Instance;
        }

        /// <summary>
        /// Retrieves the <see cref="LogManager" /> from the <paramref name="owinContext" />.
        /// </summary>
        /// <param name="owinContext">An <see cref="IOwinContext" /> for the current request.</param>
        /// <returns></returns>
        public static LogManager GetLogManager(this IOwinContext owinContext)
        {
            Arg.NotNull(owinContext, nameof(owinContext));
#if CODECONTRACTS
            Contract.Ensures(Contract.Result<LogManager>() != null);
#endif

            LogManager logManager = owinContext.Environment.Get<LogManager>(LogManagerKey);
            if (logManager != null)
            {
                return logManager;
            }

            // If not set (eg AppBuilderExtensions.SetTracerFactory() wasn't called), return the global instance
            return LogManager.Instance;
        }

        /// <summary>
        /// Stores <paramref name="logManager" /> in the <paramref name="owinContext" />.
        /// </summary>
        /// <param name="owinContext">An <see cref="IOwinContext" /> for the current request.</param>
        /// <param name="logManager">The <see cref="LogManager" /> to store.</param>
        /// <returns></returns>
        internal static void SetLogManager(this IOwinContext owinContext, LogManager logManager)
        {
            Arg.NotNull(owinContext, nameof(owinContext));
            Arg.NotNull(logManager, nameof(logManager));

            if (owinContext.Environment.ContainsKey(LogManagerKey))
            {
                var tracer = owinContext.GetTracerFactory().GetTracer(typeof(LogJamManagerMiddleware));
                if (ReferenceEquals(logManager, owinContext.GetLogManager()))
                {
                    tracer.Warn("Same LogManager instance was stored in IOwinContext more than once (skipping). Is more than one LogJamManagerMiddleware instance in the OWIN pipeline?");
                }
                else
                {
                    tracer.Error("Different LogManager instance was stored in IOwinContext (skipping). This can occur if multiple OWIN pipelines are created, and OWIN Startup is run more than once - see https://github.com/logjam2/logjam/issues/22.");
                }
            }
            else
            {
                owinContext.Environment.Add(LogManagerKey, logManager);
            }
        }

        /// <summary>
        /// Stores <paramref name="tracerFactory" /> in the <paramref name="owinContext" />.
        /// </summary>
        /// <param name="owinContext">An <see cref="IOwinContext" /> for the current request.</param>
        /// <param name="tracerFactory">The <see cref="ITracerFactory" /> to store.</param>
        /// <returns></returns>
        internal static void SetTracerFactory(this IOwinContext owinContext, ITracerFactory tracerFactory)
        {
            Arg.NotNull(owinContext, nameof(owinContext));
            Arg.NotNull(tracerFactory, nameof(tracerFactory));

            if (owinContext.Environment.ContainsKey(TracerFactoryKey))
            {
                var owinContextExistingTracerFactory = owinContext.GetTracerFactory();
                var tracer = owinContextExistingTracerFactory.GetTracer(typeof(LogJamManagerMiddleware));
                if (ReferenceEquals(tracerFactory, owinContextExistingTracerFactory))
                {
                    tracer.Warn("Same ITracerFactory instance was stored in IOwinContext more than once (skipping). Is more than one LogJamManagerMiddleware instance in the OWIN pipeline?");
                }
                else
                {
                    tracer.Error("Different ITracerFactory instance was stored in IOwinContext (skipping). This can occur if multiple OWIN pipelines are created, and OWIN Startup is run more than once - see https://github.com/logjam2/logjam/issues/22.");
                }
            }
            else
            {
                owinContext.Environment.Add(TracerFactoryKey, tracerFactory);
            }
        }

    }

}
