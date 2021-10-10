using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;

namespace RulesMSBuild.Tools.Builder.Diagnostics.GraphViz
{
    public class DotWriter
    {
        public DotWriter(StyleMode styleMode)
        {
            _styler = styleMode switch
            {
                StyleMode.Build => new BuildStyler(),
                StyleMode.Inspect => new InspectStyler(),
                _ => throw new ArgumentOutOfRangeException(nameof(styleMode), styleMode, null)
            };
        }
        public enum StyleMode
        {
            Build,
            Inspect
        }
        private readonly StringBuilder _sb = new StringBuilder();
        private int _indentLevel;
        private readonly IStyler _styler;

        void InlineAttributes(Dictionary<string, string> attrs)
        {
            _sb.Append(" [");
            Attributes(attrs);
            _sb.AppendLine("]");
        }
        void Attributes(Dictionary<string, string> attrs)
        {
            _sb.AppendJoin(" ", attrs.Select(p => $"{p.Key}={p.Value}"));
        }

        private StringBuilder Indent()
        {
            for (int i = 0; i < _indentLevel; i++)
            {
                _sb.Append('\t');
            }

            return _sb;
        }
        public string Write(TargetGraph g)
        {
            _sb.Append("digraph g");
            Open();
            Indent();
            _sb.AppendLine("ranksep=1.8");
            _sb.AppendLine("fillcolor=grey21 style=filled");
            _sb.AppendLine("fontcolor=gray92");
            
            Indent();
            _sb.AppendLine("node [shape=box style=filled]");
                
            WriteCluster(g, g);
            
            var clusters = g.Clusters.Values.ToList();
            foreach (var cluster in clusters)
            {
                Indent().Append($"subgraph cluster_{cluster.Id}");
                Open();
                Indent();
                Attributes(new Dictionary<string, string>()
                    {["label"] = $"<{cluster.Name}<br/>{cluster.PropertiesString}>"});
                _sb.AppendLine();
                
                var externalEdges = WriteCluster(g, cluster);

                Close();

                foreach (var edge in externalEdges)
                {
                    WriteEdge(edge);
                }
            }

            Close();

            return _sb.ToString();
        }

        private List<TargetGraph.Edge> WriteCluster(TargetGraph g, Cluster cluster)
        {
            var externalEdges = new List<TargetGraph.Edge>();

            if (g != cluster && cluster.Nodes.Count == 0)
            {
                cluster.GetOrAdd("empty");
            }
            
            foreach (var node in cluster.Nodes.Values)
            {
                Indent()
                    .Append(node.Id);

                var style = _styler.GetAttrs(node);
                var attrs = new Dictionary<string, string>() {["style"] = $"\"{style.Style}\""};
            
                if (style.Fill != null)
                {
                    node.Color = style.Fill; // highlight edges with this color
                    attrs["fillcolor"] = "\"" + style.Fill + "\"";
                }
                if (style.Penwidth != null)
                    attrs["penwidth"] = style.Penwidth;
                if (style.outline != null)
                    attrs["color"] = style.outline;
                attrs["label"] = $"<{node.Name}>";
                
                InlineAttributes(attrs);

                
                foreach (var dep in node.Dependencies.Values.Cast<TargetGraph.Edge>())
                {
                    if (dep.To.Cluster != dep.From.Cluster)
                    {
                        // graphviz sometimes doesn't like it when we declare inter-cluster edges inside a cluster.
                        // sometimes it will draw the external node in the current cluster instead of making a new
                        // cluster
                        externalEdges.Add(dep);
                        continue;
                    }

                    WriteEdge(dep);
                }
            }

            return externalEdges;
        }

        private void WriteEdge(TargetGraph.Edge edge)
        {
            Indent().Append(edge.From.Id).Append(" -> ").Append(edge.To.Id);

            var edgeStyle = _styler.GetStyle(edge);
            
            var dict = new Dictionary<string, string>();
            foreach (var prop in typeof(EdgeStyle).GetFields())
            {
                if (prop.GetValue(edgeStyle) is string value)
                    dict[prop.Name] = value;
            }
            InlineAttributes(dict);
        }
        private void Open()
        {
            _indentLevel++;
            _sb.AppendLine(" {");
        }

        private void Close()
        {
            _indentLevel--;
            Indent();
            _sb.AppendLine("}");
        }
    }
}