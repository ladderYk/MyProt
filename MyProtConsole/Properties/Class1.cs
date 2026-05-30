using MyProt.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyProtConsole.Properties
{
    internal class Class1
    {
        public static IProtocolService ProtocolService { get; } = new InMemoryProtocolService();
    
    }
}
