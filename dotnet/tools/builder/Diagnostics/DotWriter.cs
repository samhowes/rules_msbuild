using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;

namespace RulesMSBuild.Tools.Builder.Diagnostics
{
    public class DotWriter
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private int _indentLevel;

        void InlineAttributes(params (string name, string value)[] attrs)
        {
            _sb.Append(" [");
            Attributes(attrs);
            _sb.AppendLine("]");
        }
        void Attributes(params (string name, string value)[] attrs)
        {
            _sb.AppendJoin(" ", attrs.Select(p => $"{p.name}={p.value}"));
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
            _sb.Append("digraph g\n{\n\tnode [shape=box style=filled]\n");
            _indentLevel++;
            
            WriteCluster(g, g);
            
            var clusters = g.Clusters.Values.ToList();
            foreach (var cluster in clusters)
            {
                Indent().AppendLine($"subgraph cluster_{cluster.Id} {{");
                _indentLevel++;
                Indent();
                Attributes(("label", $"<{cluster.Name}<br/>{cluster.PropertiesString}>"));
                _sb.AppendLine();
                
                var externalEdges = WriteCluster(g, cluster);

                _indentLevel--;
                Indent();
                _sb.AppendLine("}");

                foreach (var edge in externalEdges)
                {
                    WriteEdge(edge);
                }
            }

            _indentLevel--;
            _sb.Append("}");

            return _sb.ToString();
        }

        private List<TargetGraph.Edge> WriteCluster(TargetGraph g, Cluster cluster)
        {
            var externalEdges = new List<TargetGraph.Edge>();
            g.CachePossible.TryGetValue(cluster.Name, out var canCache);

            if (g != cluster && cluster.Nodes.Count == 0)
            {
                cluster.GetOrAdd("empty");
            }
            
            foreach (var node in cluster.Nodes.Values)
            {
                Indent().Append(node.Id);

                var nodeAttrs = new List<(string, string)>();
                nodeAttrs.Add(("label", $"<{node.Name}>"));

                string style = "filled";
                string fill = "white";
                string? penwidth = null;
                if (node.IsDuplicate) fill = "tomato";
                else if (node.EntryPoint) fill = "lightgreen";
                else if (node.WasBuilt && node.FromCache) fill = "tomato";
                else if (node.WasBuilt) fill = "aliceblue";
                else if (node.Finished)
                {
                    fill = "gray94";
                    penwidth = "0";
                }
                nodeAttrs.Add(("fillcolor", fill));

                if (node.FromCache)
                {
                    penwidth = "2.0";
                    nodeAttrs.Add(("color", "darkgoldenrod1"));
                }
                else if (node.Started && !node.Finished)
                {
                    penwidth = "2.0";
                    nodeAttrs.Add(("color", "red"));
                }

                if (canCache?.Contains(node.Name) == true)
                {
                    penwidth = "2.0";
                    nodeAttrs.Add(("color", "green"));
                    style += ",dashed";
                }
                
                if (penwidth != null)
                    nodeAttrs.Add(("penwidth", penwidth));
                
                nodeAttrs.Add(("style", $"\"{style}\""));
                InlineAttributes(nodeAttrs.ToArray());

                
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
            string? color = null;
            if (edge.WasSkipped)
                color = "darkgray";
            var attrs = new List<(string, string)>();

            if (edge.Forced)
            {
                color ??= "darkorchid";
                attrs.Add(("style", "bold"));
            }
            else if (edge.Reason == TargetBuiltReason.BeforeTargets ||
                     edge.Reason == TargetBuiltReason.AfterTargets)
            {
                color ??= "darkorange2";
                attrs.Add(("dir", "back"));
            }
            else
            {
                color ??= "blue";
            }

            attrs.Add(("color", color));
            InlineAttributes(attrs.ToArray());
        }
    }
}