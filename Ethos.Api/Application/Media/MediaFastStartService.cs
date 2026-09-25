using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Application.Media;

public class FastStartResult
{
    public bool Success { get; set; }
    public string? OutputPath { get; set; }
    public string? ErrorMessage { get; set; }
    public long FileSizeBytes { get; set; }
    public bool MoovBeforeMdat { get; set; }
}

public interface IMediaFastStartService
{
    bool IsAvailable { get; }
    string? FfmpegExecutablePath { get; }
    Task<FastStartResult> OptimizeAsync(string inputFilePath, string outputFilePath, CancellationToken cancellationToken = default);
    Task<FastStartResult> OptimizeStreamAsync(Stream inputStream, string outputFilePath, CancellationToken cancellationToken = default);
}

public class MediaFastStartService : IMediaFastStartService
{
    private readonly ILogger<MediaFastStartService> _logger;
    private readonly string? _ffmpegPath;

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_ffmpegPath) && File.Exists(_ffmpegPath);
    public string? FfmpegExecutablePath => _ffmpegPath;

    public MediaFastStartService(IConfiguration configuration, ILogger<MediaFastStartService> logger)
    {
        _logger = logger;
        _ffmpegPath = ResolveFfmpegPath(configuration);

        if (IsAvailable)
        {
            _logger.LogInformation("[MediaFastStartService] FFmpeg resolved successfully at: {Path}", _ffmpegPath);
        }
        else
        {
            _logger.LogWarning("[MediaFastStartService] FFmpeg executable not found. Video FastStart optimization will fail safely.");
        }
    }

    private static string? ResolveFfmpegPath(IConfiguration configuration)
    {
        // 1. Explicit configuration or environment variable
        var configPath = configuration["FFmpeg:ExecutablePath"] ?? Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(configPath) && File.Exists(configPath))
            return configPath;

        // 2. Linux container / App Service default paths
        var linuxPaths = new[] { "/usr/bin/ffmpeg", "/usr/local/bin/ffmpeg", "/opt/ffmpeg/bin/ffmpeg" };
        foreach (var p in linuxPaths)
        {
            if (File.Exists(p)) return p;
        }

        // 3. Application base directory runtimes (bundled portable binary)
        var baseDir = AppContext.BaseDirectory;
        var bundledWindows = Path.Combine(baseDir, "runtimes", "win-x64", "native", "ffmpeg.exe");
        if (File.Exists(bundledWindows)) return bundledWindows;

        // 4. Windows AppData / WinGet package locations
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            var wingetRoot = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(wingetRoot))
            {
                var gyanDirs = Directory.GetDirectories(wingetRoot, "Gyan.FFmpeg*");
                foreach (var gd in gyanDirs)
                {
                    var exe = Directory.GetFiles(gd, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (exe != null && File.Exists(exe)) return exe;
                }
            }

            var winApps = Path.Combine(localAppData, "Microsoft", "WindowsApps", "ffmpeg.exe");
            if (File.Exists(winApps)) return winApps;
        }

        // 5. System PATH check
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathEntries = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var exeName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        foreach (var entry in pathEntries)
        {
            var fullPath = Path.Combine(entry.Trim(), exeName);
            if (File.Exists(fullPath)) return fullPath;
        }

        return null;
    }

    public async Task<FastStartResult> OptimizeAsync(string inputFilePath, string outputFilePath, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return new FastStartResult
            {
                Success = false,
                ErrorMessage = "FFmpeg executable is not available on this host.",
            };
        }

        if (!File.Exists(inputFilePath))
        {
            return new FastStartResult
            {
                Success = false,
                ErrorMessage = $"Input file does not exist: {inputFilePath}",
            };
        }

        // Check if output file already exists and is already valid FastStart
        if (File.Exists(outputFilePath) && IsMoovBeforeMdat(outputFilePath))
        {
            _logger.LogInformation("[MediaFastStartService] Output file already exists and is valid FastStart. Reusing: {Path}", outputFilePath);
            var existingFi = new FileInfo(outputFilePath);
            return new FastStartResult
            {
                Success = true,
                OutputPath = outputFilePath,
                FileSizeBytes = existingFi.Length,
                MoovBeforeMdat = true
            };
        }

        // Check if input is already FastStart
        if (IsMoovBeforeMdat(inputFilePath))
        {
            _logger.LogInformation("[MediaFastStartService] File already has 'moov' before 'mdat'. Optimization not required.");
            File.Copy(inputFilePath, outputFilePath, overwrite: true);
            var fi = new FileInfo(outputFilePath);
            return new FastStartResult
            {
                Success = true,
                OutputPath = outputFilePath,
                FileSizeBytes = fi.Length,
                MoovBeforeMdat = true
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath!,
            Arguments = $"-v error -i \"{inputFilePath}\" -c copy -movflags +faststart \"{outputFilePath}\" -y",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var errorOutput = await errorTask;

            if (process.ExitCode != 0 || !File.Exists(outputFilePath))
            {
                _logger.LogWarning("[MediaFastStartService] FFmpeg exited with code {Code}: {Error}", process.ExitCode, errorOutput);
                if (File.Exists(outputFilePath)) File.Delete(outputFilePath);
                return new FastStartResult
                {
                    Success = false,
                    ErrorMessage = $"FFmpeg failed (code {process.ExitCode}): {errorOutput}"
                };
            }

            var outFi = new FileInfo(outputFilePath);
            if (outFi.Length == 0)
            {
                File.Delete(outputFilePath);
                return new FastStartResult
                {
                    Success = false,
                    ErrorMessage = "FFmpeg produced a 0-byte output file."
                };
            }

            // Verify resulting MP4 atom structure
            bool moovValid = IsMoovBeforeMdat(outputFilePath);
            if (!moovValid)
            {
                _logger.LogWarning("[MediaFastStartService] Output file failed 'moov before mdat' validation.");
                File.Delete(outputFilePath);
                return new FastStartResult
                {
                    Success = false,
                    ErrorMessage = "Verification failed: 'moov' atom is not positioned before 'mdat'."
                };
            }

            _logger.LogInformation("[MediaFastStartService] FastStart optimization successful. Output size: {Size:N0} bytes", outFi.Length);
            return new FastStartResult
            {
                Success = true,
                OutputPath = outputFilePath,
                FileSizeBytes = outFi.Length,
                MoovBeforeMdat = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MediaFastStartService] Exception during FastStart optimization.");
            if (File.Exists(outputFilePath))
            {
                try { File.Delete(outputFilePath); } catch { }
            }
            return new FastStartResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<FastStartResult> OptimizeStreamAsync(Stream inputStream, string outputFilePath, CancellationToken cancellationToken = default)
    {
        var tempInput = Path.Combine(Path.GetTempPath(), $"ethos_in_{Guid.NewGuid():N}.mp4");
        try
        {
            using (var tempFs = new FileStream(tempInput, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                if (inputStream.CanSeek) inputStream.Position = 0;
                await inputStream.CopyToAsync(tempFs, cancellationToken);
                if (inputStream.CanSeek) inputStream.Position = 0;
            }

            return await OptimizeAsync(tempInput, outputFilePath, cancellationToken);
        }
        finally
        {
            if (File.Exists(tempInput))
            {
                try { File.Delete(tempInput); } catch { }
            }
        }
    }

    public static bool IsMoovBeforeMdat(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            long moovOffset = -1;
            long mdatOffset = -1;
            long pos = 0;
            var header = new byte[16];

            while (pos < fs.Length)
            {
                fs.Position = pos;
                int read = fs.Read(header, 0, 8);
                if (read < 8) break;

                uint size32 = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, 4));
                string type = Encoding.ASCII.GetString(header, 4, 4);

                long boxSize = size32;

                if (size32 == 1)
                {
                    read = fs.Read(header, 8, 8);
                    if (read < 8) break;
                    boxSize = (long)BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(8, 8));
                }
                else if (size32 == 0)
                {
                    boxSize = fs.Length - pos;
                }

                if (type == "moov" && moovOffset < 0)
                {
                    moovOffset = pos;
                }
                else if (type == "mdat" && mdatOffset < 0)
                {
                    mdatOffset = pos;
                }

                if (moovOffset >= 0 && mdatOffset >= 0)
                {
                    break;
                }

                if (boxSize <= 0) break;
                pos += boxSize;
            }

            return moovOffset >= 0 && mdatOffset >= 0 && moovOffset < mdatOffset;
        }
        catch
        {
            return false;
        }
    }
}
