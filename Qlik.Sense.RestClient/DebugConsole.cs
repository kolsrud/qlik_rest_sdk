using System;

namespace Qlik.Sense.RestClient
{
    public class DebugConsole : IDisposable
    {
        public DebugConsole()
        {
            RestClient.DebugConsole = this;
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