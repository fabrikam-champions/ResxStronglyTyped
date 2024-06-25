using Microsoft.Extensions.Localization;

namespace Microsoft.AspNetCore.Builder
{
    public static class ResxStronglyTypedExtensions
    {
        public static IApplicationBuilder UseResxStronglyTyped(this IApplicationBuilder app)
        {
            var stringLocalizerFactory = (IStringLocalizerFactory)app.ApplicationServices.GetService(typeof(IStringLocalizerFactory));
            ResxStronglyTyped.Extensions.LocalizationManager.SetStringLocalizerFactory(stringLocalizerFactory);
            return app;
        }
    }
}