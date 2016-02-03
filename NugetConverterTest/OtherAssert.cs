using System;

namespace Ullink.NugetConverterTest
{
    public static class OtherAssert
    {
        public static void Thrown<T>(Action action)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T) { }
        }
    }
}
