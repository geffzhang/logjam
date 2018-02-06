﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LogJamManagerMiddleware.cs">
// Copyright (c) 2011-2018 https://github.com/logjam2.  
// </copyright>
// Licensed under the <a href="https://github.com/logjam2/logjam/blob/master/LICENSE.txt">Apache License, Version 2.0</a>;
// you may not use this file except in compliance with the License.
// --------------------------------------------------------------------------------------------------------------------


using System;
using System.Threading.Tasks;

using LogJam.Shared.Internal;
using LogJam.Trace;

using Microsoft.Owin;

namespace LogJam.Owin
{

    /// <summary>
    /// Handles startup and shutdown of logging; and stores a <see cref="LogManager" /> and <see cref="TraceManager" />
    /// instance with each request.
    /// </summary>
    internal sealed class LogJamManagerMiddleware : OwinMiddleware, IDisposable
    {

        private readonly bool _enabled;
        private readonly LogManager _logManager;
        private readonly TraceManager _traceManager;

        public LogJamManagerMiddleware(OwinMiddleware next, LogManager logManager, TraceManager traceManager)
            : base(next)
        {
            Arg.NotNull(next, nameof(next));
            Arg.NotNull(logManager, nameof(logManager));
            Arg.NotNull(traceManager, nameof(traceManager));

            _enabled = (logManager.Config.Writers.Count > 0) || (traceManager.Config.Writers.Count > 0);
            _logManager = logManager;
            _traceManager = traceManager;

            if (_enabled)
            {
                _traceManager.Start(); // Starts both TraceManager and LogManager.
            }
        }

        public override Task Invoke(IOwinContext owinContext)
        {
            if (_enabled)
            {
                owinContext.SetLogManager(_logManager);
                owinContext.SetTracerFactory(_traceManager);
            }

            return Next.Invoke(owinContext);
        }

        public void Dispose()
        {
            if (_enabled)
            {
                var tracer = _traceManager.SetupTracerFactory.TracerFor(this);
                tracer.Debug("Shutting down LogJamManagerMiddleware...");

                _logManager.Stop();
                _traceManager.Stop();
            }
        }

    }

}
