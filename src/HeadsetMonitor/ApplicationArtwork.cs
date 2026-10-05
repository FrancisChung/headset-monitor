namespace HeadsetMonitor;

internal static class ApplicationArtwork
{
    public static Icon? LoadApplicationIcon()
    {
        var executablePath = Environment.ProcessPath;
        return string.IsNullOrWhiteSpace(executablePath)
            ? null
            : Icon.ExtractAssociatedIcon(executablePath);
    }
}
