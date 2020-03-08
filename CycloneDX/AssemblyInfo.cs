[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("CycloneDX.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("CycloneDX.IntegrationTests")]

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1055:UriReturnValuesShouldNotBeStrings")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1056:UriPropertiesShouldNotBeStrings")]

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Name", "CA1720:IdentifiersShouldNotContainTypeNames")]

// TODO: in the context of a CLI tool this wasn't as much of a problem, we wanted the http client to hang around and be re-used, but needs to be revisited for the core library
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope")]

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase")]
