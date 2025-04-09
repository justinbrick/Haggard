using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Haggard.Engine.Extensions;

public static class HostBuilderExtensions
{
     public static IServiceCollection ConfigureBaseEngine(this IServiceCollection hostBuilder)
     {

          return hostBuilder;
     }
}