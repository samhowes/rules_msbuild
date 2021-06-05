using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;

namespace MyRulesDotnet.Tools.Builder
{
    public class TargetGraph
    {
        public class Node
        {
            public string Name { get; }
            public Dictionary<string, string> Properties { get; set; } = new();
            public List<Edge> Dependencies { get; } = new();
            public bool WasBuilt { get; set; }

            public Node(string name)
            {
                Name = name;
                
            }
        }
        
        public class Edge
        {
            public Node From { get; }
            public Node To { get; }
            public bool WasSkipped { get; }
            public TargetBuiltReason Reason { get; set; }

            public Edge(Node from, Node to, bool wasSkipped = false)
            {
                From = @from;
                To = to;
                WasSkipped = wasSkipped;
            }
        }

        public TargetGraph(string entryProject)
        {
            EntryProject = entryProject;
        }
        
        public string EntryProject { get; }

        public Dictionary<string, Node> Nodes = new(StringComparer.OrdinalIgnoreCase);
        public string ToDot()
        {
            var sb = new StringBuilder();

            sb.Append("digraph g\n{\n\tnode [shape=box style=filled]\n");

            foreach (var (_,node) in Nodes)
            {
                var nodeId = node.Name;
                var nodeName = nodeId;
                
                var globalPropertiesString = string.Join(
                    "<br/>",
                    node.Properties.OrderBy(kvp => kvp.Key)
                        .Select(kvp => $"{kvp.Key}={kvp.Value}"));

                sb.Append('\t')
                    .Append(nodeId)
                    .Append(" [label=<").Append(nodeName).Append("<br/>")
                    .Append(globalPropertiesString).Append(">");
                var fill = node.WasBuilt ? "aliceblue" : "white";
                sb.Append($" fillcolor={fill}");

                sb.AppendLine("]");

                foreach (var dep in node.Dependencies)
                {
                    // var dep = (Node)node.Dependencies[depKey!];
                    var referenceId = dep.To.Name;

                    sb.Append('\t').Append(nodeId).Append(" -> ").Append(referenceId);
                    var color = dep.WasSkipped ? "darkgray" : "blue";
                    var attrs = new Dictionary<string, string>();
                    
                    if (dep.Reason == TargetBuiltReason.BeforeTargets)
                    {
                        color = "darkorange2";
                        attrs["dir"] = "back";
                    }

                    attrs["color"] = color;
                    sb.Append("[");
                    sb.AppendJoin(" ", attrs.Select(p => $"{p.Key}={p.Value}"));
                    sb.AppendLine("]");
                }
            }

            sb.Append("}");

            return sb.ToString();
        }

        public Node Add(string name)
        {
            if (!Nodes.TryGetValue(name, out var node))
            {
                node = new Node(name);
                Nodes[name] = node;
            }

            return node;
        }

        public Node GetOrAdd(string name)
        {
            return Add(name);
        }
    }
}