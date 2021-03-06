﻿using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("Umbraco.Web")]
[assembly: AssemblyDescription("Umbraco Web")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyProduct("Umbraco CMS")]

[assembly: ComVisible(false)]
[assembly: Guid("ce9d3539-299e-40d3-b605-42ac423e24fa")]

// Umbraco Cms
[assembly: InternalsVisibleTo("Umbraco.Web.UI")]

[assembly: InternalsVisibleTo("Umbraco.Tests")]
[assembly: InternalsVisibleTo("Umbraco.Tests.Benchmarks")]

[assembly: InternalsVisibleTo("Umbraco.VisualStudio")] // fixme - what is this?
[assembly: InternalsVisibleTo("Umbraco.ModelsBuilder")] // fixme - why?
[assembly: InternalsVisibleTo("Umbraco.ModelsBuilder.AspNet")] // fixme - why?

// Allow this to be mocked in our unit tests
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

// Umbraco Deploy
[assembly: InternalsVisibleTo("Umbraco.Deploy")]
[assembly: InternalsVisibleTo("Umbraco.Deploy.UI")]
[assembly: InternalsVisibleTo("Umbraco.Deploy.Cloud")]

// Umbraco Forms
[assembly: InternalsVisibleTo("Umbraco.Forms.Core")]
[assembly: InternalsVisibleTo("Umbraco.Forms.Core.Providers")]
[assembly: InternalsVisibleTo("Umbraco.Forms.Web")]

// v8
[assembly: InternalsVisibleTo("Umbraco.Compat7")]

