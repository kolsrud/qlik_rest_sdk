using System;

namespace Qlik.Sense.RestClient
{
    public class DebugConsole : IDisposable
    {
#if (NETSTANDARD2_0)
        private const string dotnet_version = ".NET Core 2.1";
#else
        private const string dotnet_version = ".NET Framework 4.5.2";
#endif

        public DebugConsole()
        {
            RestClient.DebugConsole = this;
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
                RestClient.DebugConsole = null;
            }
        }
    }
}