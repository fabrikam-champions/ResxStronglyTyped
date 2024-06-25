using Microsoft.Extensions.Localization;

namespace ResxStronglyTyped.Extensions
{
    public static class LocalizationManager
    {
        private static IStringLocalizerFactory _stringLocalizerFactory;
        public static void SetStringLocalizerFactory(IStringLocalizerFactory stringLocalizerFactory)
        {
            _stringLocalizerFactory = stringLocalizerFactory;
        }
        public static IStringLocalizer GetStringLocalizer<T>()
        {
            return _stringLocalizerFactory.Create(typeof(T));
        }
    }
}