namespace CycloneDX.Models
{

    public static class NugetInputFactory
    {
        public static NugetInputModel Create(string baseUrl, string baseUrlUserName, string baseUrlUserPassword,
            bool isPasswordClearText)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(baseUrlUserName) && !string.IsNullOrEmpty(baseUrlUserPassword))
            {
                return new NugetInputModel(baseUrl, baseUrlUserName, baseUrlUserPassword, isPasswordClearText);
            }
            return null;
        }

    }

    public class NugetInputModel
    {
        public string nugetFeedUrl { get; set; }
        public string nugetUsername { get; set; }
        public string nugetPassword { get; set; }
        public bool IsPasswordClearText { get; set; }

        public NugetInputModel(string baseUrl, string baseUrlUserName, string baseUrlUserPassword,
            bool isPasswordClearText)
        {
            nugetFeedUrl = baseUrl;
            nugetUsername = baseUrlUserName;
            nugetPassword = baseUrlUserPassword;
            IsPasswordClearText = isPasswordClearText;
        }
    }
}
