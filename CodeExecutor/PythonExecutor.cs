using System.Runtime.InteropServices;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using Spectre.Console;

namespace CodeExecutor;

public class PythonExecutor
{
    private const string ImageName = "python";
    private const string Tag = "3.9-slim";

    /// <summary>
    /// Executes python code in docker.
    /// </summary>
    /// <param name="code">Python code</param>
    /// <returns>Output</returns>
    public async Task<string> ExecuteCodeInDocker(string code)
    {
        try
        {
            var tempFilePath = WriteCodeToFile(code);

            var dockerUri = GetDockerUri();

            AnsiConsole.MarkupLine($"[cyan]Using Docker URI:[/] {dockerUri}");

            using var dockerClient = new DockerClientConfiguration(new Uri(dockerUri))
                .CreateClient();

            AnsiConsole.MarkupLine("[green]Docker client initialized.[/]");

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Pinging Docker...", async ctx => { await dockerClient.System.PingAsync(); });

            AnsiConsole.MarkupLine("[green]Docker connectivity verified.[/]");
            
            await PullImageIfNotExists(dockerClient, ImageName, Tag);

            var createParams = new CreateContainerParameters
            {
                Image = $"{ImageName}:{Tag}",
                Name =  $"python-executor-{Guid.NewGuid()}",
                Cmd = ["python", "/code/script.py"],
                HostConfig = new HostConfig
                {
                    Binds =
                    [
                        $"{tempFilePath}:/code/script.py" // Mount the Python file into the container
                    ],
                    AutoRemove = true,
                    Memory = 256 * 1024 * 1024,
                    CPUShares = 512
                }
            };

            AnsiConsole.MarkupLine("[cyan]Creating Docker container...[/]");

            var response = await dockerClient.Containers.CreateContainerAsync(createParams);

            AnsiConsole.MarkupLine($"[green]Container created with ID:[/] {response.ID}");

            var started = await dockerClient.Containers.StartContainerAsync(response.ID, null);
            if (!started)
            {
                AnsiConsole.MarkupLine("[red]Failed to start Docker container.[/]");
                return "Failed to start Docker container.";
            }

            AnsiConsole.MarkupLine("[green]Docker container started.[/]");

            AnsiConsole.MarkupLine("[cyan]Fetching container logs...[/]");

            var logStream = await dockerClient.Containers.GetContainerLogsAsync(response.ID, false,
                new ContainerLogsParameters
                {
                    ShowStdout = true,
                    ShowStderr = true,
                    Follow = true
                });

            var logs = await ReadOutputAsync(logStream);

            Cleanup(tempFilePath);

            return logs;
        }
        catch (DockerApiException apiEx)
        {
            AnsiConsole.MarkupLine($"[red]Docker API error:[/] {apiEx.Message}");
            return $"Docker API error: {apiEx.Message}";
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Execution failed:[/] {ex.Message}");
            return $"Execution failed: {ex.Message}";
        }
    }

    private string GetDockerUri()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "npipe://./pipe/docker_engine";
        }

        return "unix:///var/run/docker.sock";
    }

    private async Task PullImageIfNotExists(DockerClient client, string imageName, string tag)
    {
        try
        {
            var filters = new Dictionary<string, IDictionary<string, bool>>
            {
                { "reference", new Dictionary<string, bool> { { $"{imageName}:{tag}", true } } }
            };

            AnsiConsole.MarkupLine($"[cyan]Checking if image '{imageName}:{tag}' exists locally...[/]");

            var images = await client.Images.ListImagesAsync(new ImagesListParameters()
            {
                All = true,
                Filters = filters
            });

            if (images.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]Image '{imageName}:{tag}' not found locally. Pulling the image...[/]");

                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Pulling Docker image...", async ctx =>
                    {
                        await client.Images.CreateImageAsync(
                            new ImagesCreateParameters
                            {
                                FromImage = imageName,
                                Tag = tag
                            },
                            null,
                            new Progress<JSONMessage>(message =>
                            {
                                if (!string.IsNullOrEmpty(message.Status))
                                {
                                    AnsiConsole.MarkupLine($"[grey]{message.Status}[/]");
                                }
                            })
                        );
                    });

                AnsiConsole.MarkupLine($"[green]Image '{imageName}:{tag}' pulled successfully.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Image '{imageName}:{tag}' already exists locally.[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error while pulling image '{imageName}:{tag}': {ex.Message}[/]");
            throw;
        }
    }

    private async Task<string> ReadOutputAsync(MultiplexedStream multiplexedStream,
        CancellationToken cancellationToken = default)
    {
        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);

        var response = new StringBuilder();
        try
        {
            while (true)
            {
                Array.Clear(buffer, 0, buffer.Length);

                var readResult =
                    await multiplexedStream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);

                if (readResult.EOF)
                {
                    break;
                }

                if (readResult.Count > 0)
                {
                    var responseLine = Encoding.UTF8.GetString(buffer, 0, readResult.Count);
                    response.AppendLine(responseLine.Trim());
                }
                else
                {
                    break;
                }
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }

        return response.ToString();
    }

    private static void Cleanup(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                AnsiConsole.MarkupLine("[green]Temporary file deleted successfully.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Temporary file does not exist. No need to delete.[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to delete temporary file: {ex.Message}[/]");
        }
    }

    private string WriteCodeToFile(string code)
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"UserCode_{Guid.NewGuid()}.py");
        File.WriteAllText(tempFilePath, code);

        AnsiConsole.MarkupLine($"[blue]Python script written to:[/] {tempFilePath}");

        return tempFilePath;
    }
}