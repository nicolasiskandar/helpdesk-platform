using System.Diagnostics;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Gateway.Middleware;

public class TraceContextTransform : ITransformProvider
{
    public void ValidateRoute(TransformRouteValidationContext context) { }
    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(transformContext =>
        {
            var activity = Activity.Current;
            if (activity?.Id != null)
            {
                transformContext.ProxyRequest.Headers.Remove("traceparent");
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("traceparent", activity.Id);

                if (!string.IsNullOrEmpty(activity.TraceStateString))
                {
                    transformContext.ProxyRequest.Headers.Remove("tracestate");
                    transformContext.ProxyRequest.Headers.TryAddWithoutValidation("tracestate", activity.TraceStateString);
                }
            }

            return ValueTask.CompletedTask;
        });
    }
}
