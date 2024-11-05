using Docker.DotNet.Models;
using Docker.DotNet;
using VideoConverter.Classes;

namespace VideoConverter
{
    internal class VideoConverterDockerService(DockerInfo dockerInfo)
    {

        private const string ContainerDirectoryInput = "/source"; // Path inside the container
        private const string ContainerDirectoryOutput = "/destination"; // Path inside the container

        async Task ListImages()
        {
            // Create a DockerClient
            using var dockerClient = CreateDockerClient();
            // List all images
            var images = await dockerClient.Images.ListImagesAsync(new ImagesListParameters()
            {
                All = true // Set to true to get all images, even intermediate layers
            });

            var containers = await dockerClient.Containers.ListContainersAsync(new ContainersListParameters
            {
                All = false // Only list running containers
            });


            // Display image details
            foreach (var image in images)
            {
                var imageName = string.Join(", ", image.RepoTags);

                Console.WriteLine($"ID: {image.ID}");
                Console.WriteLine($"RepoTags: {string.Join(", ", image.RepoTags)}");
                Console.WriteLine($"Size: {image.Size} bytes");
                Console.WriteLine($"Created: {image.Created}");

                Console.WriteLine(new string('-', 20));

                int containerCount = containers.Count(x => imageName.Contains(x.Image));

                Console.WriteLine($"Number of containers running from image '{imageName}': {containerCount}");
            }

            Console.ReadKey();
            Console.ReadKey();
        }

        async Task PullImage()
        {
            // Create a DockerClient
            using var dockerClient = CreateDockerClient();
            try
            {
                var progress = new Progress<JSONMessage>();
                progress.ProgressChanged += PullImageProgressChanged;

                // Pull the image
                await dockerClient.Images.CreateImageAsync(new ImagesCreateParameters
                {
                    FromImage = dockerInfo.ImageName,
                    Tag = dockerInfo.ImageTag // Specify the tag if needed
                }, new AuthConfig(), progress);
                Console.WriteLine("Image pulled successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pulling image: {ex.Message}");
            }
        }

        private static void PullImageProgressChanged(object? sender, JSONMessage e)
        {
            Console.WriteLine(e.Status);
        }

        public async Task RunAndRemoveImage()
        {
            // Create the Docker client (for local Docker daemon)
            using var dockerClient = CreateDockerClient();

            try
            {
                #region Create the container
                /*
                    In this first stage, a container is made from an image. 
                    It's not running yet, and no resources are given to it. 
                    The container stays in this stage until you start it.
                 */
                var imageName = $"{dockerInfo.ImageName}:{dockerInfo.ImageTag}";

                var createContainerResponse =
                    await dockerClient.Containers
                        .CreateContainerAsync(new CreateContainerParameters
                        {
                            Image = "video_converter:v4", // Image to use
                            Name = Guid.NewGuid().ToString(), // Name of the container
                            // Cmd = new[] { "PRESET", "INPUT_FILE" }, // Command arguments
                            Cmd = new[] { "fast", "input_test.mp4" }, // Command arguments
                            HostConfig = new HostConfig
                            {
                                // Set memory limit to 512MB (512 * 1024 * 1024 bytes)
                                Memory = 1L * 1024 * 1024 * 1024, // 1G RAM

                                // Set CPU limit to 50% of a single core
                                NanoCPUs = 4000_000_000, // 1 CPU (half core, where 1 CPU = 1,000,000,000 NanoCPUs)

                                Binds = new List<string> // Volume bindings
                                {
                                    // Format: host_path:container_path
                                    $"{dockerInfo.HostDirectoryInput}:{ContainerDirectoryInput}",
                                    $"{dockerInfo.HostDirectoryOutput}:{ContainerDirectoryOutput}"
                                },
                                AutoRemove = true, // Equivalent to --rm (auto-remove container after it exits)
                            }
                        });

                if (createContainerResponse == null || string.IsNullOrEmpty(createContainerResponse.ID))
                {
                    Console.WriteLine("Container creation failed.");
                    return;
                }
                else
                {
                    Console.WriteLine($"Container created with ID: {createContainerResponse.ID}");
                }

                #endregion

                #region Start the container (in detached mode)
                /*
                    When you start a container, it moves to the Running stage.
                    Here, the container is working and doing its tasks. 
                    It stays in this stage until you pause, stop, or delete it.                 
                 */
                var started = await dockerClient.Containers.StartContainerAsync(createContainerResponse.ID,
                    new ContainerStartParameters());

                #endregion

                // Optionally, wait for the container to exit if you need to
                var containerWaitResponse = await dockerClient.Containers.WaitContainerAsync(createContainerResponse.ID);

                #region After Remove Container
                Console.WriteLine($"containerWaitResponse.StatusCode is: {containerWaitResponse.StatusCode}");
                Console.WriteLine(containerWaitResponse.Error?.Message);
                #endregion
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private DockerClient CreateDockerClient()
        {
            return new DockerClientConfiguration(new Uri(dockerInfo.DockerUri ?? throw new InvalidOperationException()))
                .CreateClient();
        }

        async Task MonitorContainerStatus(DockerClient client, string containerId)
        {
            string previousStatus = "";

            // Continuously check the container status
            while (true)
            {
                // Get the container details
                var containers = await client.Containers.ListContainersAsync(new ContainersListParameters()
                {
                    All = true // Include stopped containers
                });

                var container = containers.FirstOrDefault(c => c.ID.StartsWith(containerId) || c.Names.Contains($"/{containerId}"));

                if (container != null)
                {
                    string currentStatus = container.Status;

                    // Only log the status change when the status is different
                    if (currentStatus != previousStatus)
                    {
                        Console.WriteLine($"Container Status Changed: {currentStatus}");
                        previousStatus = currentStatus;
                    }

                    // Check if the container has gone into "dead" status
                    if (currentStatus.Contains("dead"))
                    {
                        Console.WriteLine($"ALERT: Container {containerId} has gone to 'dead' status!");
                        break; // Exit the monitoring loop
                    }
                }
                else
                {
                    Console.WriteLine($"Container {containerId} not found.");
                    break;
                }

                // Wait for a few seconds before checking again
                await Task.Delay(5000);
            }
        }
    }
}
