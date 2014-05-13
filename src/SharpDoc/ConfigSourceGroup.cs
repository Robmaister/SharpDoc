﻿// Copyright (c) 2010-2013 SharpDoc - Alexandre Mutel
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

using System.Collections.Generic;
using System.Xml.Serialization;

namespace SharpDoc
{
    /// <summary>
    /// A list of assembly to load into the same group API.
    /// </summary>
    public class ConfigSourceGroup
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigSourceGroup"/> class.
        /// </summary>
        public ConfigSourceGroup()
        {
            Sources = new List<ConfigSource>();
            SearchDirectories = new List<string>();
        }

        /// <summary>
        /// Gets or sets the merge group.
        /// </summary>
        /// <value>
        /// The merge group.
        /// </value>
        [XmlAttribute("api")]
        public string MergeGroup { get; set; }

        /// <summary>
        /// Gets or sets the additional search directories for reference assemblies.
        /// </summary>
        /// <value>The search directories.</value>
        [XmlElement("searchdir")]
        public List<string> SearchDirectories { get; set; }

        /// <summary>
        /// Gets or sets a list of source file (assembly or xml comment file).
        /// </summary>
        /// <value>The sources file.</value>
        [XmlElement("source")]
        public List<ConfigSource> Sources { get; set; }
    }
}