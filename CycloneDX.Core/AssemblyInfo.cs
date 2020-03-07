[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("CycloneDX.Core.Tests")]
// TODO: in the context of a CLI tool this wasn't as much of a problem, we wanted the http client to hang around and be re-used, but needs to be revisited for the core library
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters")]