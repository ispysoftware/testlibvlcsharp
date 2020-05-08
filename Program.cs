using System;
using System.Threading.Tasks;

namespace TestLibVLC
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await new TestVLC().Run();
        }
    }
}
