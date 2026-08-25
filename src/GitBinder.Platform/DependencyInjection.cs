using GitBinder.Application.Common;
using GitBinder.Application.Security;
using GitBinder.Platform.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.Versioning;

namespace GitBinder.Platform;

/// <summary>
/// Platform 层依赖注入注册（Windows / macOS / Linux 按运行时选择）。
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPlatform(this IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
        {
            AddWindowsPlatformServices(services);
        }
        else if (OperatingSystem.IsMacOS())
        {
            services.AddSingleton<IPathNormalizer, WindowsPathNormalizer>(); // V1 仅 Windows，占位。
        }
        else
        {
            services.AddSingleton<IPathNormalizer, WindowsPathNormalizer>(); // V1 仅 Windows，占位。
        }

        return services;
    }

    [SupportedOSPlatform("windows")]
    private static void AddWindowsPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IPathNormalizer, WindowsPathNormalizer>();
        services.AddSingleton<IApplicationDataPath, WindowsApplicationDataPath>();
        services.AddSingleton<IShellService, WindowsShellService>();

        // Secret Store 依赖 IApplicationDataPath 提供存储目录。
        services.AddSingleton<ISecretStore>(sp =>
        {
            var dataPath = sp.GetRequiredService<IApplicationDataPath>();
            return new WindowsDpapiSecretStore(Path.Combine(dataPath.DataDirectory, "secrets"));
        });
    }
}
