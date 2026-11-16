using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RSMA.Microcontroller.Virtual
{
    /// <summary>
    /// Emits a stable, dependency-free JSON representation for CI artifacts.
    /// Property order and numeric formatting are intentionally fixed.
    /// </summary>
    public static class VirtualTraceJsonExporter
    {
        public static string Export(IList<VirtualTraceEvent> events, ulong droppedEventCount = 0)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));

            var builder = new StringBuilder();
            builder.Append("{\"droppedEventCount\":");
            builder.Append(droppedEventCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"events\":[");
            for (var index = 0; index < events.Count; index++)
            {
                if (index != 0)
                    builder.Append(',');
                AppendEvent(builder, events[index]);
            }
            builder.Append("]}");
            return builder.ToString();
        }

        public static string Export(VirtualTraceBuffer trace)
        {
            if (trace == null)
                throw new ArgumentNullException(nameof(trace));
            return Export(trace.Snapshot(), trace.DroppedEventCount);
        }

        private static void AppendEvent(StringBuilder builder, VirtualTraceEvent traceEvent)
        {
            if (traceEvent == null)
                throw new ArgumentException("Trace collection contains null event", nameof(traceEvent));

            builder.Append("{\"kind\":");
            AppendString(builder, traceEvent.Kind.ToString());
            builder.Append(",\"sequence\":");
            builder.Append(traceEvent.Sequence.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"timestampMilliseconds\":");
            builder.Append(traceEvent.TimestampMilliseconds.ToString("R", CultureInfo.InvariantCulture));
            builder.Append(",\"channel\":");
            builder.Append(traceEvent.Channel.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"value\":");
            builder.Append(traceEvent.Value.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"auxiliaryValue\":");
            builder.Append(traceEvent.AuxiliaryValue.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"message\":");
            if (traceEvent.Message == null)
                builder.Append("null");
            else
                AppendString(builder, traceEvent.Message);
            builder.Append('}');
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (var character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            builder.Append(character);
                        break;
                }
            }
            builder.Append('"');
        }
    }
}