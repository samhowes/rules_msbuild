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
        public HashSet<string> Dupes { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                node.IsDuplicate = !Dupes.Add(name);
            }

            return node;
        }
    }
    
    public class TargetGraph : Cluster
    {
        private readonly string _trimPath;

        private Dictionary<string, HashSet<string>>
            Duplicates = new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public class Node
        {
            public string Name { get; }
            public List<Edge> Dependencies { get; } = new();
            public bool WasBuilt { get; set; }
            public bool EntryPoint { get; set; }
            public bool FromCache { get; set; }
            public Cluster? Cluster { get; set; }
            public string Id { get; }
            public bool Finished { get; set; }
            public bool IsDuplicate { get; set; }

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

        public string TrimPath(string p) => p.Replace(_trimPath, "");
        public string ToDot() => new DotWriter().Write(this);

        public Cluster GetOrAddCluster(string name, IDictionary<string, string>? properties) 
        {
            var cluster = new Cluster(name, properties);
            if (!Clusters.TryGetValue(cluster.UniqueName, out var existing))
            {
                cluster.Id = Clusters.Count + 1;
                Clusters[cluster.UniqueName] = cluster;
                Cached.TryGetValue(name, out var cache);
                if (!Duplicates.TryGetValue(name, out var dupes))
                {
                    dupes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    Duplicates[name] = dupes;
                }

                cluster.Dupes = dupes;
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

        public void CanCache(string projectFullPath, ICollection<string> targetNames)
        {
            var name = TrimPath(projectFullPath);
            if (!CachePossible.TryGetValue(name, out var canCache))
            {
                canCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CachePossible[name] = canCache;
            }

            foreach (var targetName in targetNames)
                canCache.Add(targetName);
        }

        public Dictionary<string, HashSet<string>> CachePossible { get; } =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
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
            
            WriteCluster(g, g);
            
            var clusters = g.Clusters.Values.ToList();
            foreach (var cluster in clusters)
            {
                Indent().AppendLine($"subgraph cluster_{cluster.Id} {{");
                _indentLevel++;
                Indent();
                Attributes(("label", $"<{cluster.Name}<br/>{cluster.PropertiesString}>"));
                _sb.AppendLine();
                
                WriteCluster(g, cluster);

                _indentLevel--;
                Indent();
                _sb.AppendLine("}");
            }

            _indentLevel--;
            _sb.Append("}");

            return _sb.ToString();
        }

        private void WriteCluster(TargetGraph g, Cluster cluster)
        {
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
                else if (!node.Finished)
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