#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using RulesMSBuild.Tools.Builder.Diagnostics.GraphViz;

namespace RulesMSBuild.Tools.Builder.Diagnostics
{
    public class Cluster
    {
        public string Name { get; }
        public IDictionary<string, string>? Properties { get; }
        public string UniqueName { get; set; }
        public string PropertiesString { get; set; } = "";
        public int Id { get; set; }
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
            return false;
            if (properties == null) return false;
            PropertiesString = string.Join(";", properties.OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Value}"));
            UniqueName = Name + ";" + PropertiesString;
            return true;
        }

        public TargetGraph.Node GetOrAdd(string name)
        {
            name = TargetGraph.Node.CleanName(name);
            if (!Nodes.TryGetValue(name, out var node))
            {
                var id = TargetGraph.Node.CleanId(name + Id);
                node = new TargetGraph.Node(name, id)
                {
                    Cluster = this,
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
            public OrderedDictionary Dependencies { get; } = new();
            public bool WasBuilt => Started && Finished;
            public bool FromCache { get; set; }
            public Cluster? Cluster { get; set; }
            public string Id { get; }
            public bool Started { get; set; }
            public bool Finished { get; set; }
            public string? Color { get; set; }
            public bool ColorEdges { get; set; } = true;
            public bool CachePoint { get; set; }
            public bool Error { get; set; }
            public bool ReferencedExternally { get; set; }

            public Node(string name, string id)
            {
                Name = name;
                Id = id;
            }

            public Edge AddDependency(Node node, TargetBuiltReason? targetBuiltReason)
            {
                Edge edge;
                if (!Dependencies.Contains(node.Id))
                {
                    edge = new Edge(this, node){Reason = targetBuiltReason};
                    if (!targetBuiltReason.HasValue)
                        edge.Forced = true;
                    Dependencies[edge.To.Id] = edge;
                }
                else
                {
                    edge = (Edge)Dependencies[node.Id]!;
                }
                    
                return edge;
            }

            public static string CleanName(string name)
            {
                return name.Trim();
            }
            public static string CleanId(string id)
            {
                return BadCharsRegex.Replace(id, "_");
            }

            private static Regex BadCharsRegex =
                new Regex(@"[.\$\@\(\)%]", RegexOptions.Compiled | RegexOptions.Multiline);
        }
        
        public class Edge
        {
            public Node From { get; }
            public Node To { get; }
            public bool WasSkipped { get; }
            public TargetBuiltReason? Reason { get; set; }
            public bool Forced { get; set; }
            public bool ShouldCache { get; set; }
            public bool Runtime { get; set; }

            public Edge(Node from, Node to, bool wasSkipped = false)
            {
                From = @from;
                To = to;
                WasSkipped = wasSkipped;
            }
        }
        public Dictionary<string, Cluster> Clusters { get; } = new(StringComparer.OrdinalIgnoreCase);

        public TargetGraph(string trimPath, string projectPath, IDictionary<string, string>? properties)
            : base(projectPath.Replace(trimPath, ""), properties)
        {
            _trimPath = trimPath;
            Id = 1;
        }

        public string TrimPath(string p) => p.Replace(_trimPath, "");
        public string ToDot(DotWriter.StyleMode styleMode = DotWriter.StyleMode.Build) 
            => new DotWriter(styleMode).Write(this);

        public Cluster GetOrAddCluster(string name, IDictionary<string, string>? properties = null) 
        {
            var cluster = new Cluster(name, properties);
            if (!Clusters.TryGetValue(cluster.UniqueName, out var existing))
            {
                cluster.Id = Clusters.Count + 1;
                Clusters[cluster.UniqueName] = cluster;
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
    }
}