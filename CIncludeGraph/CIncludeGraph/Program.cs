﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.GraphModel;
using System.IO;

namespace CIncludeGraph
{
    class Program
    {
        Graph graph = new Graph();
        List<string> includePaths = new List<string>();
        Queue<string> stack = new Queue<string>();
        HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void Main(string[] args)
        {
            Program p = new Program();
            if (p.ParseCommandLine(args))
            {
                p.Process();
            }
            else
            {
                p.PrintUsage();
            }
        }

        private void PrintUsage()
        {
            Console.WriteLine("CIncludeGraph [-I includepath] [files]");
        }

        bool ParseCommandLine(string[] args)
        {
            for (int i = 0, n = args.Length; i < n; i++)
            {
                string arg = args[i];
                if (arg[0] == '-' || arg[0] == '/')
                {
                    switch (arg.Substring(1).ToLowerInvariant())
                    {
                        case "i":
                            if (i + 1 < n)
                            {
                                includePaths.Add(args[++i]);
                            }
                            break;
                        case "?":
                        case "h":
                        case "help":
                            return false;
                        default:
                            Console.WriteLine("### Error: unknown argument '{0}'", arg);
                            return false;
                    }
                }
                else
                {
                    // this supports wildcard expansion.
                    foreach (var file in Directory.GetFiles(Path.GetDirectoryName(arg), Path.GetFileName(arg)))
                    {
                        stack.Enqueue(file);
                    }
                }
            }
            if (stack.Count == 0)
            {
                Console.WriteLine("### Error: no input files specified");
                return false;
            }
            return true;
        }

        private void Process()
        {
            while (stack.Count > 0)
            {
                string file = stack.Dequeue();
                if (!visited.Contains(file))
                {
                    visited.Add(file);
                    ProcessFile(file);
                }
            }

            graph.Save("includes.dgml");
            Console.WriteLine("Generated 'includes.dgml'");
        }

        private void ProcessFile(string file)
        {
            Console.WriteLine(file);
            using (StreamReader reader = new StreamReader(file))
            {
                string line = reader.ReadLine();
                while (line != null)
                {
                    line = line.Trim();
                    if (line.StartsWith("#include"))
                    {
                        ProcessInclude(file, line);
                    }
                    line = reader.ReadLine();
                }
            }
        }

        private void ProcessInclude(string file, string line)
        {
            bool ws = true;
            bool start = false;
            char quote = '\0';
            StringBuilder sb = new StringBuilder();
            for (int i = 8, n = line.Length; i < n; i++)
            {
                char ch = line[i];
                if (!start)
                {
                    if (ch != ' ' && ch != '\t')
                    {
                        if (ws)
                        {
                            return; // must be inside a comment or something
                        }
                        quote = (ch == '<' ? '>' : ch);
                        start = true;
                    }
                    else if (ws)
                    {
                        // good.
                        ws = false;
                    }
                }
                else if (ch != quote)
                {
                    sb.Append(ch);
                }
                else
                {
                    break;
                }
            }

            // see if it is relative to the file itself.
            Uri baseUri = new Uri(file);
            string rel = sb.ToString();
            string resolved = null;
            if (!ResolveUri(baseUri, rel, out resolved))
            {
                // use include paths to find it.
                foreach (string path in includePaths)
                {
                    baseUri = new Uri(path + "/");
                    if (ResolveUri(baseUri, rel, out resolved))
                    {
                        break;
                    }
                }
            }

            if (resolved != null)
            {
                GraphNode source = graph.Nodes.GetOrCreate(file, Path.GetFileName(file), null);
                GraphNode target = graph.Nodes.GetOrCreate(resolved, Path.GetFileName(resolved), null);
                graph.Links.GetOrCreate(source, target);
                stack.Enqueue(resolved);
            }
            else
            {
                Console.WriteLine("### error: include not found '{0}' from file '{1}'", rel, file);
            }


        }

        private bool ResolveUri(Uri baseUri, string rel, out string resolved)
        {
            resolved = null;
            Uri uri = new Uri(baseUri, rel);
            if (File.Exists(uri.LocalPath))
            {
                resolved = uri.LocalPath;
                return true;
            }
            return false;
        }
    }
}
