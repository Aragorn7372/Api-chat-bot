using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace ApiChatbot.Infraestructure;

/// <summary>
/// Configuración de Serilog para logging estructurado.
/// Configura la salida a consola con formato personalizado y filtros de nivel por namespace.
/// </summary>
public static class SerilogConfig
{
    /// <summary>
    /// Configura Serilog con salida a consola y filtros de nivel personalizados.
    /// </summary>
    /// <returns>Configuración de logger lista para usar como bootstrap logger.</returns>
    /// <remarks>
    /// Filtros de nivel:
    /// <list type="bullet">
    ///   <item><description>Nivel mínimo global: Information</description></item>
    ///   <item><description>Microsoft: Warning (reduce ruido)</description></item>
    ///   <item><description>Microsoft.Hosting.Lifetime: Information</description></item>
    ///   <item><description>Microsoft.EntityFrameworkCore.Database.Command: Warning</description></item>
    /// </list>
    /// Formato de salida: [HH:mm:ss NIV] Mensaje + Excepción
    /// </remarks>
    public static LoggerConfiguration Configure()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                theme: AnsiConsoleTheme.Code);
    }
}