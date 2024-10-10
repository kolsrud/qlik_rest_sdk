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
            RestClientGeneric.RestClientDebugConsole = this;
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
                RestClientGeneric.RestClientDebugConsole = null;
            }
        }
    }
}