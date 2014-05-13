// Copyright (c) 2010-2013 SharpDoc - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using SharpDoc.Logging;
using SharpDoc.Model;

namespace SharpDoc
{
    /// <summary>
    /// Mono Cecil implementation of <see cref="IAssemblyManager"/>.
    /// </summary>
    internal class MonoCecilAssemblyManager : IAssemblyManager
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MonoCecilAssemblyManager"/> class.
        /// </summary>
        public MonoCecilAssemblyManager()
        {
            AssemblySources = new List<NAssemblySource>();
            AssemblyReferences = new List<AssemblyDefinition>();
        }

        private List<AssemblyDefinition> AssemblyReferences { get; set; }

        private List<NAssemblySource> AssemblySources { get; set; }

        /// <summary>
        /// Loads all assembly sources/xml doc and references
        /// </summary>
        public List<NAssemblySource> Load(Config config)
        {
            var configPath = Path.GetDirectoryName(Path.GetFullPath(config.FilePath));
            configPath = configPath ?? Environment.CurrentDirectory;
            // Load all sources
            foreach (var group in config.Groups)
            {
                group.MergeGroup = group.MergeGroup ?? "default";
                foreach (var source in group.Sources)
                {
                    // Setup full path
                    if (!string.IsNullOrEmpty(source.AssemblyPath))
                        source.AssemblyPath = Path.Combine(configPath, source.AssemblyPath);

                    if (!string.IsNullOrEmpty(source.DocumentationPath))
                        source.DocumentationPath = Path.Combine(configPath, source.DocumentationPath);

                    Load(group, source);
                }
            }

            var finalSources = new List<NAssemblySource>();

            // Check that all source assemblies have valid Xml associated with it
            foreach (var assemblySource in AssemblySources.Where(node => node.Assembly != null))
            {
                int countXmlDocFound = 0;
                NDocumentApi docFound = null;
                string assemblyName = ((AssemblyDefinition) assemblySource.Assembly).Name.Name;

                var docSources = new List<NAssemblySource>();
                if (assemblySource.Document != null)
                    docSources.Add(assemblySource);
                docSources.AddRange( AssemblySources.Where(node => node.Assembly == null) );

                foreach (var doc in docSources)
                {
                    var node = doc.Document.Document.SelectSingleNode("/doc/assembly/name");
                    if (assemblyName == node.InnerText.Trim())
                    {
                        docFound = doc.Document;
                        countXmlDocFound++;
                    }
                }

                if (countXmlDocFound == 0)
                    Logger.Fatal("Unable to find documentation for assembly [{0}]", assemblyName);
                else if (countXmlDocFound > 1)
                    Logger.Fatal("Cannot load from multiple ([{0}]) documentation sources for assembly [{1}]", countXmlDocFound, assemblyName);

                assemblySource.Document = docFound;
                finalSources.Add(assemblySource);

            }
            return finalSources;
        }

        private HashSet<string> searchPaths = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        private void Load(ConfigSourceGroup group, ConfigSource configSource)
        {
            NAssemblySource assemblySource = null;

            if (configSource.AssemblyPath == null)
            {
                Logger.Error("Assembly path not specified for <source>. Cannot be null");
                return;
            }

            // Check Parameters
            if (!File.Exists(configSource.AssemblyPath))
            {
                Logger.Error("Assembly file [{0}] not found", configSource.AssemblyPath);
                return;
            }

            var extension = Path.GetExtension(configSource.AssemblyPath);
            if (extension != null && (extension.ToLower() == ".dll" || extension.ToLower() == ".exe"))
            {
                assemblySource = LoadAssembly(group, configSource.AssemblyPath);
                AssemblySources.Add(assemblySource);
            }
            else
            {
                Logger.Fatal("Invalid Assembly source [{0}]. Must be either an Assembly", configSource.AssemblyPath);
            }

            // If documentation path is null, use by default the assembly path
            if (configSource.DocumentationPath == null)
            {
                configSource.DocumentationPath = Path.ChangeExtension(configSource.AssemblyPath, ".xml");
            }

            if (!File.Exists(configSource.DocumentationPath))
            {
                Logger.Error("Documentation file [{0}] not found", configSource.DocumentationPath);
                return;
            }

            extension = Path.GetExtension(configSource.DocumentationPath);
            if (extension != null && extension.ToLower() == ".xml")
            {
                if (assemblySource == null)
                {
                    assemblySource = new NAssemblySource();
                    AssemblySources.Add(assemblySource);
                }

                assemblySource.Document = LoadAssemblyDocumentation(configSource.DocumentationPath);
            }
            else
            {
                Logger.Fatal("Invalid Assembly source [{0}]. Must be either a Xml comment file", configSource.DocumentationPath);
            }
        }

        private NAssemblySource LoadAssembly(ConfigSourceGroup group, string source)
        {
            var dirPath = Path.GetDirectoryName(source);
            var assemblyResolver = new DefaultAssemblyResolver();
            // Remove any default search path
            assemblyResolver.RemoveSearchDirectory(".");
            assemblyResolver.RemoveSearchDirectory("bin");

            // Search from assembly directory
            assemblyResolver.AddSearchDirectory(dirPath);

            // Add additional search directory
            foreach (var searchDirectory in group.SearchDirectories)
            {
                assemblyResolver.AddSearchDirectory(searchDirectory);
            }

            var parameters = new ReaderParameters(ReadingMode.Immediate) { AssemblyResolver = assemblyResolver };

            assemblyResolver.ResolveFailure += (sender, reference) =>
            {
                var searchDirectories = assemblyResolver.GetSearchDirectories();
                foreach (var directory in searchDirectories)
                {
                    var tryPath = Path.Combine(directory, reference.Name + ".winmd");
                    if (!File.Exists(tryPath))
                        continue;

                    try
                    {
                        var winmdAssembly = AssemblyDefinition.ReadAssembly(tryPath, parameters);
                        if (winmdAssembly != null)
                            return winmdAssembly;
                    }
                    catch
                    {
                        // Failed... fall thru and try the next one.
                    }
                }

                // Log an error if we can't find the assembly. Mono.Cecil will throw an exception just after returning from 
                // this callback
                Logger.Error("Failed to resolve {0}", reference.FullName);
                return null;
            };

            var assemblyDefinition = AssemblyDefinition.ReadAssembly(source, parameters);
            var assemblySource = new NAssemblySource(assemblyDefinition)
            {
                Filename = source,
                MergeGroup = group.MergeGroup
            };
            return assemblySource;
        }

        private NDocumentApi LoadAssemblyDocumentation(string source)
        {
            var xmlDoc = NDocumentApi.Load(source);

            var node = xmlDoc.Document.SelectSingleNode("/doc/assembly/name");
            if (node == null)
                Logger.Fatal("Not valid xml documentation for source [{0}]", source);

            return xmlDoc;
        }
    }
}