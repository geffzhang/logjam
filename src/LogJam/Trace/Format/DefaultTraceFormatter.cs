﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DefaultTraceFormatter.cs">
// Copyright (c) 2011-2015 https://github.com/logjam2.  
// </copyright>
// Licensed under the <a href="https://github.com/logjam2/logjam/blob/master/LICENSE.txt">Apache License, Version 2.0</a>;
// you may not use this file except in compliance with the License.
// --------------------------------------------------------------------------------------------------------------------


namespace LogJam.Trace.Format
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;

    using LogJam.Format;


    /// <summary>
    /// The debugger trace formatter.
    /// </summary>
    public class DefaultTraceFormatter : EntryFormatter<TraceEntry>
    {

        public DefaultTraceFormatter()
        {}

        #region Public Properties

        /// <summary>
        /// <c>true</c> to include the Date when formatting <see cref="TraceEntry" />s.
        /// </summary>
        public bool IncludeDate { get; set; }

        /// <summary>
        /// <c>true</c> to include the Timestamp when formatting <see cref="TraceEntry" />s.
        /// </summary>
        public bool IncludeTimestamp { get; set; }

        /// <summary>
        /// We don't yet support indenting based on context, but we do support indenting a constant amount. 
        /// </summary>
        public int IndentLevel { get; set; }

        #endregion

        #region Formatter methods

        /// <summary>
        /// Formats the trace entry for debugger windows
        /// </summary>
        public override string Format(ref TraceEntry traceEntry)
        {
            //int indentSpaces = 0;

            var sw = new StringWriter();
            var newLine = sw.NewLine;
            var newLineLength = newLine.Length;

            //if (TraceManager.Config.ActivityTracingEnabled)
            //{
            //	// Compute indent spaces based on current ActivityRecord scope
            //}

            //sw.Repeat(' ', indentSpaces);

            sw.Write("{0,-7}\t", traceEntry.TraceLevel);

            if (IncludeTimestamp)
            {
#if PORTABLE
				DateTime outputTime = TimeZoneInfo.ConvertTime(timestampUtc, _outputTimeZone);
#else
                DateTime outputTime = TimeZoneInfo.ConvertTimeFromUtc(traceEntry.TimestampUtc, _outputTimeZone);
#endif
                // TODO: Implement own formatting to make this more efficient
                sw.Write(outputTime.ToString("HH:mm:ss.fff\t"));
            }

            var message = traceEntry.Message.Trim();
            sw.Write("{0,-50}     {1}", traceEntry.TracerName, message);
            if (! message.EndsWith(newLine))
            {
                sw.WriteLine();
            }

            if (traceEntry.Details != null)
            {
                //sw.Repeat(' ', indentSpaces);
                string detailsMessage = traceEntry.Details.ToString();
                sw.Write(detailsMessage);
                if (! detailsMessage.EndsWith(newLine))
                {
                    sw.WriteLine();
                }
            }

            return sw.ToString();
        }

        public override void Format(ref TraceEntry traceEntry, FormatterWriter writer)
        {
            ColorCategory color = ColorCategory.None;
            if (writer.IsColorEnabled)
            {
                color = TraceLevelToColorCategory(traceEntry.TraceLevel);
            }

            writer.BeginEntry(IndentLevel);

            if (IncludeDate)
            {
                writer.WriteDate(traceEntry.TimestampUtc);
            }
            if (IncludeTimestamp)
            {
                writer.WriteTimestamp(traceEntry.TimestampUtc);
            }
            writer.WriteField(TraceLevelToLabel(traceEntry.TraceLevel), color, 7);
            writer.WriteAbbreviatedTypeName(traceEntry.TracerName, ColorCategory.Detail, 50);
            writer.WriteField(traceEntry.Message.Trim(), color);
            if (traceEntry.Details != null)
            {
                writer.WriteLines(traceEntry.Details.ToString(), ColorCategory.Detail, 1);
            }

            writer.EndEntry();
        }

        #endregion

        protected ColorCategory TraceLevelToColorCategory(TraceLevel traceLevel)
        {
            switch (traceLevel)
            {
                case TraceLevel.Info:
                    return ColorCategory.Info;
                case TraceLevel.Verbose:
                    return ColorCategory.Detail;
                case TraceLevel.Debug:
                    return ColorCategory.Debug;
                case TraceLevel.Warn:
                    return ColorCategory.Warning;
                case TraceLevel.Error:
                case TraceLevel.Severe:
                    return ColorCategory.Failure;
                default:
                    return ColorCategory.None;
            }
        }

        protected string TraceLevelToLabel(TraceLevel traceLevel)
        {
            switch (traceLevel)
            {
                case TraceLevel.Info:
                    return "Info";
                case TraceLevel.Verbose:
                    return "Verbose";
                case TraceLevel.Debug:
                    return "Debug";
                case TraceLevel.Warn:
                    return "Warn";
                case TraceLevel.Error:
                    return "Error";
                case TraceLevel.Severe:
                    return "SEVERE";
                default:
                    return "";
            }
        }

    }
}
