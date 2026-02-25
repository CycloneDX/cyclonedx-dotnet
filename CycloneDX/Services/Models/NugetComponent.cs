using System.Collections.Generic;

namespace CycloneDX.Services.Models
{
    public class NugetComponent
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public NugetComponentScope? Scope { get; set; }
        public string Purl { get; set; }
        public NugetComponentType Type { get; set; }
        public string BomRef { get; set; }
        public List<NugetHash> Hashes { get; set; }
        public List<NugetLicense> Licenses { get; set; }
        public List<NugetContact> Authors { get; set; }
        public string Copyright { get; set; }
        public string Description { get; set; }
        public List<NugetExternalReference> ExternalReferences { get; set; }
        public List<NugetVulnerability> Vulnerabilities { get; set; }
    }

    public enum NugetComponentScope
    {
        Required,
        Optional,
        Excluded
    }

    public enum NugetComponentType
    {
        Library
    }

    public class NugetHash
    {
        public string Algorithm { get; set; }
        public string Content { get; set; }
    }

    public class NugetLicense
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
    }

    public class NugetContact
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
    }

    public class NugetVulnerability
    {
        public string AdvisoryUrl { get; set; }
        public int Severity { get; set; }
    }

    public class NugetExternalReference
    {
        public NugetExternalReferenceType Type { get; set; }
        public string Url { get; set; }
    }

    public enum NugetExternalReferenceType
    {
        Website,
        Vcs,
        Chat,
        Documentation,
        Support,
        Distribution,
        License,
        Other
    }
}
