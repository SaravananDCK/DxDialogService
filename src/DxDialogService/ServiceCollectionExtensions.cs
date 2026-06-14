using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DxDialogService
{
    /// <summary>
    /// Registration helpers for the DxDialogService dialog service.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="DialogService"/> as a scoped service.
        /// Place a single <c>&lt;DxDialogHost /&gt;</c> in your main layout.
        /// Requires the DevExpress Blazor services to be registered separately (<c>AddDevExpressBlazor</c>).
        /// </summary>
        public static IServiceCollection AddDxDialogService(this IServiceCollection services)
        {
            services.TryAddScoped<DialogService>();
            return services;
        }
    }
}
