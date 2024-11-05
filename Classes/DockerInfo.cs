namespace VideoConverter.Classes
{
    public class DockerInfo
    {
        public string? DockerUri { get; set; }
        public string? ImageName { get; set; }
        public string? ImageTag { get; set; }
        public string? HostDirectoryInput { get; set; }
        public string? HostDirectoryOutput { get; set; }
    }
}
