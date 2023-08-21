using System;

namespace Qlik.Sense.RestClient
{
    public class RestClientDebugConsole : IDisposable
    {
#if (NETCOREAPP2_1)
        private const string dotnet_version = ".NET Core 2.1";
#elif (NETCOREAPP3_1)
        private const string dotnet_version = ".NET Core 3.1";
#elif (NET452)
        private const string dotnet_version = ".NET Framework 4.5.2";
#elif (NET462)
        private const string dotnet_version = ".NET Framework 4.6.2";
#elif (NET7_0)
        private const string dotnet_version = ".NET Framework 7.0";
#endif

        public RestClientDebugConsole()
        {
            RestClient.RestClientDebugConsole = this;
            Log("Debug console activated (" + dotnet_version + ")");
        }

        public void Log(string message)
        {
            Console.WriteLine(message);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                RestClient.RestClientDebugConsole = null;
            }
        }
    }

    [Obsolete("Class deprecated due to name clash with Qlik Sense .NET SDK. Use class RestClientDebugConsole instead.")] // Obsolete since January 2022
    public class DebugConsole : RestClientDebugConsole
    {
        [Obsolete("Class deprecated due to name clash with Qlik Sense .NET SDK. Use class RestClientDebugConsole instead.")] // Obsolete since January 2022
        public DebugConsole() : base()
        {
        }
    }
}