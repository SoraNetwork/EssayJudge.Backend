using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using SoraEssayJudge.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Linq;

namespace SoraEssayJudge.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatusController : ControllerBase
    {
        private readonly EssayContext _context;
        private readonly ILogger<StatusController> _logger;
        private readonly IHostEnvironment _environment;

        public StatusController(EssayContext context, ILogger<StatusController> logger, IHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> GetStatus()
        {
            string dbStatus;
            try
            {
                dbStatus = await _context.Database.CanConnectAsync() ? "Connected" : "Disconnected";
                _logger.LogInformation("Database connection status: {DbStatus}", dbStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connection check failed.");
                dbStatus = $"Error: {ex.Message}";
            }

            var process = Process.GetCurrentProcess();
            var informationalVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            
            var version = informationalVersion ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "N/A";
            var gitCommit = "N/A";

            // 尝试从 AssemblyInformationalVersion (例如 "1.0.0+a1b2c3d") 中解析 git commit
            // 此信息由 GitHub Actions 在构建时注入
            if (!string.IsNullOrEmpty(informationalVersion) && informationalVersion.Contains('+'))
            {
                var parts = informationalVersion.Split('+');
                version = parts[0]; // 版本号部分
                if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                {
                    gitCommit = parts[1];
                    if (gitCommit.Length > 7)
                    {
                        gitCommit = gitCommit.Substring(0, 7); // 取前7位
                    }
                }
            }

            var status = new
            {
                ServerStatus = "Running",
                ServerTimeUtc = DateTime.UtcNow,
                Uptime = (DateTime.UtcNow - process.StartTime.ToUniversalTime()).ToString(@"d\.hh\:mm\:ss"),
                
                Build = new {
                    Version = version,
                    GitCommit = gitCommit,
                },

                Application = new {
                    Environment = _environment.EnvironmentName,
                    Framework = RuntimeInformation.FrameworkDescription,
                    ProcessId = process.Id,
                    MemoryUsage = $"{process.WorkingSet64 / 1024 / 1024:N2} MB",
                    TotalAllocatedMemory = $"{GC.GetTotalMemory(false) / 1024 / 1024:N2} MB",
                    ThreadCount = process.Threads.Count,
                },

                System = new {
                    HostName = Environment.MachineName,
                    ServerIpAddresses = GetServerIpAddresses(),
                    OS = RuntimeInformation.OSDescription,
                    OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                    ProcessorCount = Environment.ProcessorCount,
                },

                Request = new {
                    ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "N/A"
                },
                
                DatabaseStatus = dbStatus
            };

            return Ok(status);
        }

        private string GetServerIpAddresses()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ipAddresses = host.AddressList
                    .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) // 筛选 IPv4 地址
                    .Select(ip => ip.ToString())
                    .ToList();

                return ipAddresses.Any() ? string.Join(", ", ipAddresses) : "N/A";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve server IP addresses.");
                return "Error retrieving IPs";
            }
        }
    }
}
