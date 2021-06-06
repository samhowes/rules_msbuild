#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;

namespace MyRulesDotnet.Tools.Builder
{
    public class Cluster
    {
        public string Name { get; }
        public IDictionary<string, string>? Properties { get; }
        public string UniqueName { get; set; }
        public string PropertiesString { get; set; } = "";
        public int Id { get; set; }
        public HashSet<string> Cache { get; set; } = null!;
        public Dictionary<string, TargetGraph.Node> Nodes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Cluster(string name, IDictionary<string,string>? properties)
        {
            Name = name;
            UniqueName = name;
            Properties = properties;
            SetProperties(properties);
        }

        public bool SetProperties(IDictionary<string, string>? properties)
        {
            if (properties == null) return false;
            PropertiesString = string.Join(";", properties.OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Value}"));
            UniqueName = Name + ";" + PropertiesString;
            return true;
        }

        public TargetGraph.Node GetOrAdd(string name)
        {
            if (!Nodes.TryGetValue(name, out var node))
            {
                node = new TargetGraph.Node(name, name + Id)
                {
                    Cluster = this,
                    FromCache = Cache.Contains(name)
                };
                Nodes[name] = node;
            }

            return node;
        }
    }
    
    public class TargetGraph : Cluster
    {
        private readonly string _trimPath;

        public class Node
        {
            public string Name { get; }
            public List<Edge> Dependencies { get; } = new();
            public bool WasBuilt { get; set; }
            public bool EntryPoint { get; set; }
            public bool FromCache { get; set; }
            public Cluster? Cluster { get; set; }
            public string Id { get; }

            public Node(string name, string id)
            {
                Name = name;
                Id = id;
            }
        }
        
        public class Edge
        {
            public Node From { get; }
            public Node To { get; }
            public bool WasSkipped { get; }
            public TargetBuiltReason Reason { get; set; }
            public bool Forced { get; set; }

            public Edge(Node from, Node to, bool wasSkipped = false)
            {
                From = @from;
                To = to;
                WasSkipped = wasSkipped;
            }
        }
        public Dictionary<string, Cluster> Clusters { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, HashSet<string>> Cached { get; } =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public TargetGraph(string trimPath, string projectPath, IDictionary<string, string>? properties)
            : base(projectPath.Replace(trimPath, ""), properties)
        {
            _trimPath = trimPath;
            Id = 1;
            Cache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Cached[Name] = Cache;
        }
        public string ToDot() => new DotWriter().Write(this);

        public Cluster GetOrAddCluster(string name, IDictionary<string, string>? properties) 
        {
            var cluster = new Cluster(name, properties);
            if (!Clusters.TryGetValue(cluster.UniqueName, out var existing))
            {
                cluster.Id = Clusters.Count + 1;
                Clusters[cluster.UniqueName] = cluster;
                Cached.TryGetValue(name, out var cache);
                cluster.Cache = cache ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                cluster = existing;
                var oldName = cluster.UniqueName;
                if (cluster.SetProperties(properties))
                {
                    Clusters.Remove(oldName);
                    Clusters[cluster.UniqueName] = cluster;
                }
            }

            return cluster;
        }


        public void AddCached(string projectPath, string targetName)
        {
            var trimmed = projectPath.Replace(_trimPath, "");
            if (!Cached.TryGetValue(trimmed, out var targets))
            {
                targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Cached[trimmed] = targets;
            }

            targets.Add(targetName);
        }
    }

    public class DotWriter
    {
        private StringBuilder _sb = new StringBuilder();
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

        StringBuilder Indent()
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
            
            WriteCluster(g);
            
            var clusters = g.Clusters.Values.ToList();
            foreach (var cluster in clusters)
            {
                Indent().AppendLine($"subgraph cluster_{cluster.Id} {{");
                _indentLevel++;
                Indent();
                Attributes(("label", $"<{cluster.Name}<br/>{cluster.PropertiesString}>"));
                _sb.AppendLine();
                
                WriteCluster(cluster);

                _indentLevel--;
                Indent();
                _sb.AppendLine("}");
            }

            _indentLevel--;
            _sb.Append("}");

            return _sb.ToString();
        }

        private void WriteCluster(Cluster cluster)
        {
            foreach (var node in cluster.Nodes.Values)
            {
                Indent().Append(node.Id);

                var nodeAttrs = new List<(string, string)>();
                nodeAttrs.Add(("label", $"<{node.Name}>"));

                string fill = "white";
                if (node.EntryPoint) fill = "chartreuse2";
                else if (node.WasBuilt && node.FromCache) fill = "tomato";
                else if (node.WasBuilt) fill = "aliceblue";
                nodeAttrs.Add(("fillcolor", fill));

                if (node.FromCache)
                {
                    nodeAttrs.Add(("penwidth", "2.0"));
                    nodeAttrs.Add(("color", "darkgoldenrod1"));
                }

                InlineAttributes(nodeAttrs.ToArray());

                foreach (var dep in node.Dependencies)
                {
                    Indent().Append(node.Id).Append(" -> ").Append(dep.To.Id);
                    string? color = null;
                    if (dep.WasSkipped)
                        color = "darkgray";
                    var attrs = new List<(string, string)>();

                    if (dep.Forced)
                    {
                        color ??= "darkorchid";
                        attrs.Add(("style", "bold"));
                    }
                    else if (dep.Reason == TargetBuiltReason.BeforeTargets ||
                             dep.Reason == TargetBuiltReason.AfterTargets)
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
    }
}