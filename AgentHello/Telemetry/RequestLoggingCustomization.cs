using Serilog.AspNetCore;

namespace AgentHello.Telemetry;

public static class RequestLoggingCustomization
{
    public static void Configure(RequestLoggingOptions options)
    {
        options.EnrichDiagnosticContext = (dc, http) =>
        {
            var act = System.Diagnostics.Activity.Current;
            if (act != null)
            {
                dc.Set("TraceId", act.TraceId.ToString());
                dc.Set("SpanId", act.SpanId.ToString());
                dc.Set("ParentSpanId", act.ParentSpanId.ToString());
            }

            dc.Set("RequestMethod", http.Request.Method);
            dc.Set("RequestPath", http.Request.Path);
            dc.Set("StatusCode", http.Response.StatusCode);
            dc.Set("Scheme", http.Request.Scheme);
            dc.Set("Host", http.Request.Host.Host);
        };
    }
}