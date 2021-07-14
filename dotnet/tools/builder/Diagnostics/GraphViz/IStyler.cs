using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;

namespace RulesMSBuild.Tools.Builder.Diagnostics.GraphViz
{
    public interface IStyler
    {
        NodeStyle GetAttrs(TargetGraph.Node node);
        EdgeStyle GetStyle(TargetGraph.Edge edge);
    }
    
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public struct EdgeStyle
    {
        public string color;
        public string penwidth;
        public string style;
        public string dir;
    }

    public class InspectStyler : IStyler
    {
        public NodeStyle GetAttrs(TargetGraph.Node node)
        {
            var style = new NodeStyle();
            string? penwidth = null;
            if (node.CachePoint)
            {
                style.Penwidth = "8.0";
                style.outline ??= "purple";
            }

            return style;
        }

        public EdgeStyle GetStyle(TargetGraph.Edge edge)
        {
            throw new System.NotImplementedException();
        }
    }

    public struct NodeStyle
    {
        public string Style;
        public string? Fill;
        public string? Penwidth;
        public string? outline;
    }
    
    public class BuildStyler : IStyler
    {
        public NodeStyle GetAttrs(TargetGraph.Node node)
        {
            var style = new NodeStyle() {Style = "filled"};

            // was this node was actively evaluated for this build?
            if (node.Finished)
            {
                // was it skipped?
                if (!node.WasBuilt)
                {
                    if (node.FromCache)
                    {
                        // likely skipped because of cache
                        style.Fill = "darkgoldenrod1";
                    }
                    else
                    {
                        // skipped because of condition
                        style.Fill = "powderblue";
                    }
                }
                else
                {
                    // it was actually built
                    if (node.FromCache)
                    {
                        // bad! we already built this from a prior build
                        style.outline = "tomato";
                    }
                    else
                    {
                        // green is good: this target was supposed to be built in this config
                        style.Fill = "lightgreen";
                    }
                }
            }
            
            if (node.FromCache && style.Fill == null)
            {
                // make sure we can see all the nodes that were in cache
                style.Fill = "lightgoldenrod1";
                node.ColorEdges = false;
            }

            return style;
        }

        public EdgeStyle GetStyle(TargetGraph.Edge edge)
        {
            var edgeStyle = new EdgeStyle();
            
            if (edge.WasSkipped)
                edgeStyle.color = "darkgray";

            if (edge.Forced)
            {
                edgeStyle.color ??= "darkorchid";
                edgeStyle.style = "bold";
            }
            else if (edge.Reason == TargetBuiltReason.BeforeTargets ||
                     edge.Reason == TargetBuiltReason.AfterTargets)
            {
                edgeStyle.color ??= "darkorange2";
                edgeStyle.dir = "back";
            }
            else
            {
                edgeStyle.color ??= "blue";
            }

            
            if (edge.ShouldCache)
            {
                edgeStyle.penwidth = "2.0";
                edgeStyle.color = "darkgoldenrod1";
            }

            edgeStyle.color = edge.Runtime && edge.From.ColorEdges && edge.From.Color != null
                ? edge.From.Color : "gray51";
            return edgeStyle;
        }
    }
}