namespace CycloneDX
{
    enum ExitCode
    {
        OK,
        InvalidOptions,
        SolutionOrProjectFileParameterMissing,
        OutputDirectoryParameterMissing,
        GitHubParameterMissing,
        InvalidGitHubApiCredentials,
        GitHubApiRateLimitExceeded
    }
}