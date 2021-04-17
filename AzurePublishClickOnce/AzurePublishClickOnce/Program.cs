﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Walkabout.Utilities;

namespace FtpPublishClickOnce
{
    class Program
    {
        string source;
        string connectionString;
        string target;

        static void Main(string[] args)
        {
            Program p = new Program();
            if (!p.ParseCommandLine(args))
            {
                PrintUsage();
            }
            else
            {
                p.Run();
            }
        }

        private void Run()
        {
            try
            {
                CleanLocalFolder(source);
                Folder sourceFolder = new Folder(source);
                Folder targetFolder = new Folder(target, connectionString);

                // trim all but the latest local version.
                Folder appFiles = sourceFolder.GetSubfolder("Application Files");
                if (appFiles == null)
                {
                    throw new Exception("The local folder doesn't contain 'Application Files' ???");
                }

                List<FileVersion> versions = new List<FileVersion>(appFiles.ChildFolders.Select(it => new FileVersion(it)));
                versions.Sort();
                while (versions.Count > 1)
                {
                    var f = versions[0];
                    versions.RemoveAt(0);
                    appFiles.GetSubfolder(f.name).DeleteSubtree();
                }

                if (!targetFolder.ChildFolders.Contains("Application Files") && (targetFolder.ChildFolders.Count != 0 || targetFolder.Files.Count != 0))
                {
                    throw new Exception("The blob folder contains something else other than 'Application Files' are you sure you have the right folder?");
                }

                sourceFolder.MirrorDirectory(targetFolder, true);
                sourceFolder.MirrorDirectory(targetFolder, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine("### Error: " + ex.Message);
            }
        }

        private void CleanLocalFolder(string source)
        {
            var appFiles = Path.Combine(source, "Application Files");
            if (Directory.Exists(appFiles))
            {
                List<string> toRemove = new List<string>();
                Version latest = null;
                string path = null;
                foreach(var folder in Directory.GetDirectories(appFiles))
                {
                    toRemove.Add(folder);
                    Version v = GetVersion(Path.GetFileName(folder));
                    if (latest == null || v > latest)
                    {
                        latest = v;
                        path = folder;
                    }
                }
                toRemove.Remove(path);
                foreach(var folder in toRemove)
                {
                    Directory.Delete(folder, true);
                }
            }
        }

        private Version GetVersion(string folder)
        {
            List<string> parts = new List<string>(folder.Split('_'));
            parts.RemoveAt(0);
            return new Version(string.Join(".", parts));
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: AzurePublishClickOnce SourceFolder TargetFolder connectionString");
            Console.WriteLine("Copies all files and directories from the source directory to the target directory");
            Console.WriteLine("using Azure blob credentials found in the BlobContainerUri.  It also cleans up 'Application Files'");
            Console.WriteLine("versions after the copy to ensure local and remote folders only contain the latest verion.");
            Console.WriteLine();
            Console.WriteLine("The BlobContainerUri should look something like this:");
            Console.WriteLine("https://somwhere.blob.core.windows.net/downloads?sv=...&st=...&se=...&sr=c&sp=...&sig=...");
            Console.WriteLine("You will probably need to put double quotes around the http Uri for it to become a valid command line argument");
        }

        private bool ParseCommandLine(string[] args)
        {
            for (int i = 0, n = args.Length; i < n; i++)
            {
                string arg = args[i];
                if (arg[0] == '-')
                {
                    switch (arg.TrimStart('-').ToLowerInvariant())
                    {
                        default:
                        case "?":
                        case "h":
                        case "help":
                            return false;
                    }
                }
                else if (source == null)
                {
                    source = arg;
                }
                else if (target == null)
                {
                    target = arg;
                    if (!target.Contains('/'))
                    {
                        Console.WriteLine("### Error: target folder should be prefixed with container name, separated by '/' ");
                    }
                }
                else if (connectionString == null)
                {
                    connectionString = arg;
                }
                else
                {
                    Console.WriteLine("### Error: too many arguments");
                    PrintUsage();
                    return false;
                }
            }

            if (source == null)
            {
                Console.WriteLine("### Error: missing source folder");
                return false;
            }

            if (!Directory.Exists(source))
            {
                Console.WriteLine("### Error: source folder does not exist: " + source);
                return false;
            }

            if (target == null)
            {
                Console.WriteLine("### Error: missing target folder");
                return false;
            }

            if (connectionString == null)
            {
                Console.WriteLine("### Error: missing blob storage connectionString");
                return false;
            }

            return true;
        }

        class FileVersion : IComparable<FileVersion>
        {
            public string name;
            public Version version;

            public FileVersion(string name)
            {
                string[] parts = name.Split('_');
                if (parts.Length < 2)
                {
                    throw new Exception("Unknown Application Files versions");
                }
                this.name = name;
                this.version = new Version(string.Join(".", parts.Skip(1)));
            }

            public int CompareTo(FileVersion other)
            {
                return this.version.CompareTo(other.version);
            }
        }
    }
}
