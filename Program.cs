using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minio;
using VideoConverter.Classes;
using Yashil.Common.SharedKernel.Helpers;

namespace VideoConverter;

class Program
{

    private static ServiceProvider? _serviceProvider;

    static Task Main()
    {
        try
        {
            _serviceProvider = ConfigureServices();

            if (_serviceProvider == null)
            {
                throw new Exception("Service Provider is null");
            }

            // Get an instance of RabbitMqService from IoC
            var rabbitMqService = _serviceProvider.GetRequiredService<IRabbitMqService>();

            // Listen for RabbitMQ messages and start work
            rabbitMqService.ListenForMessages(
                async _ =>
                    await StartConvertVideoProcess(_serviceProvider));

            // Keep the app running endlessly
            while (true)
            {
                Thread.Sleep(100); // Avoid high CPU usage, keep listening
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private static async Task<bool> StartConvertVideoProcess(ServiceProvider serviceProvider)
    {

        // Resolve MinioService and call its methods
        var minioService = serviceProvider.GetRequiredService<MinioService>();
        await minioService.UploadDirectoryAsync("lms-videos", "docs");
        return true;
    }

    /// <summary>
    /// Create instance of DockerInfo class for work with docker
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    private static DockerInfo InitialiseDockerInfo()
    {
        try
        {

            string dockerUri = Environment.GetEnvironmentVariable("DOCKER_URI")
                               ?? throw new ArgumentNullException($"DOCKER_URI environment variable is not set.");
            string imageName = Environment.GetEnvironmentVariable("IMAGE_NAME")
                               ?? throw new ArgumentNullException($"IMAGE_NAME environment variable is not set.");
            string imageTag = Environment.GetEnvironmentVariable("IMAGE_TAG")
                              ?? throw new ArgumentNullException($"IMAGE_TAG environment variable is not set.");
            string hostDirectoryInput = Environment.GetEnvironmentVariable("HOST_DIRECTORY_INPUT")
                                        ?? throw new ArgumentNullException(
                                            $"HOST_DIRECTORY_INPUT environment variable is not set.");
            string hostDirectoryOutput = Environment.GetEnvironmentVariable("HOST_DIRECTORY_OUTPUT")
                                         ?? throw new ArgumentNullException(
                                             $"HOST_DIRECTORY_OUTPUT environment variable is not set.");
            return new DockerInfo
            {
                DockerUri = dockerUri,
                ImageName = imageName,
                ImageTag = imageTag,
                HostDirectoryInput = hostDirectoryInput,
                HostDirectoryOutput = hostDirectoryOutput
            };

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    /// determine if app work with docker or not
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    private static AppConfiguration InitConfiguration()
    {
        bool enableMinio = Convert.ToBoolean(Environment.GetEnvironmentVariable("ENABLE_MINIO")
                                             ?? throw new ArgumentNullException(
                                                 $"ENABLE_MINIO environment variable is not set."));

        string encryptedConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
                                             ?? throw new ArgumentNullException(
                                                 $"CONNECTION_STRING environment variable is not set.");

        var connectionString = CryptographyHelper.AesDecrypt(encryptedConnectionString);
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("Cannot Decrypt Connection String");
        }
        AppConfiguration appConfig = new AppConfiguration
        {
            EnableMinio = enableMinio,
            ConnectionString = connectionString
        };
        return appConfig;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private static ServiceProvider ConfigureServices()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        var appConfig = InitConfiguration();
        // Setup Dependency Injection
        var serviceCollection = new ServiceCollection()
            .AddLogging(configure =>
            {
                // Set up logging configuration
                configure.AddConsole(); // Add more logging providers if needed (e.g., Debug, File, etc.)
                configure.SetMinimumLevel(LogLevel.Information); // Adjust logging level
            })
            .Configure<MinioSetting>(configuration.GetSection("MinioSetting"))
            .AddSingleton<IRabbitMqService, RabbitMqService>()
            .AddSingleton(InitialiseDockerInfo())
            .AddSingleton(appConfig);

        if (appConfig.EnableMinio)
        {
            serviceCollection.AddSingleton(provider =>
                {
                    // Manually read environment variables and create a MinioSettings instance
                    var minioSettings = new MinioSetting
                    {
                        AccessKey = Environment.GetEnvironmentVariable("Minio_AccessKey") ?? throw new Exception("Minio_AccessKey environment variable is not set."),
                        SecretKey = Environment.GetEnvironmentVariable("Minio_SecretKey") ?? throw new Exception("Minio_SecretKey environment variable is not set."),
                        Endpoint = Environment.GetEnvironmentVariable("Minio_Endpoint") ?? throw new Exception("Minio_Endpoint environment variable is not set.")
                    };

                    // Return the MinioClient configured with these settings
                    return new MinioClient()
                        .WithEndpoint(minioSettings.Endpoint)
                        .WithCredentials(minioSettings.AccessKey, minioSettings.SecretKey)
                        .Build();
                })
                .AddSingleton<MinioService>();// Register MinioService
        }


        return serviceCollection.BuildServiceProvider();
    }
}