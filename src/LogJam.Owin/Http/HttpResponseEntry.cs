﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="HttpResponseEntry.cs">
// Copyright (c) 2011-2016 https://github.com/logjam2. 
// </copyright>
// Licensed under the <a href="https://github.com/logjam2/logjam/blob/master/LICENSE.txt">Apache License, Version 2.0</a>;
// you may not use this file except in compliance with the License.
// --------------------------------------------------------------------------------------------------------------------


namespace LogJam.Owin.Http
{
    using System;
    using System.Collections.Generic;

    using LogJam.Writer.Text;


    /// <summary>
    /// A log entry that records an HTTP response.
    /// </summary>
    [DefaultFormatter(typeof(HttpResponseFormatter))]
    public struct HttpResponseEntry : ILogEntry
    {

        /// <summary>
        /// Monotonically increasing request number - starts from 1 when the webapp is started.
        /// </summary>
        public long RequestNumber;

        /// <summary>
        /// When the HTTP request completed processing.
        /// </summary>
        public DateTimeOffset RequestCompleted;

        /// <summary>
        /// Time-to-first-byte
        /// </summary>
        public TimeSpan Ttfb;

        /// <summary>
        /// The HTTP request method, eg GET, POST, etc.
        /// </summary>
        public string Method;

        /// <summary>
        /// The HTTP request URI.
        /// </summary>
        public string Uri;

        /// <summary>
        /// HTTP response status code - eg 200, 404, etc.
        /// </summary>
        public short HttpStatusCode;

        /// <summary>
        /// The HTTP status reason phrase, that often accompanies <see cref="HttpStatusCode" />.
        /// </summary>
        public string HttpReasonPhrase;

        /// <summary>
        /// The HTTP response headers.
        /// </summary>
        public KeyValuePair<string, string[]>[] ResponseHeaders;

    }

}
