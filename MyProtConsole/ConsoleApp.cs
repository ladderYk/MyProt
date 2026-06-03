using MyProtWeb;
using MyProtWeb.Services;
using MyProtWeb.webapi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyProtConsole
{
    public class ConsoleApp
    {
        public async Task InitAsync()
        {
            new App().InitAsync();
            ProtocolGateway gateway = new ProtocolGateway("./Protocols", "./tags.json");
            DeviceManager dm = new DeviceManager(gateway.getDeviceList(), gateway.getListAsync());

            PollingEngine pe = new PollingEngine(dm, gateway.getTagList(), new TagReader(dm, gateway.getTagList()), null);
            await pe.StartAsync(new CancellationToken());
        }
    }
}
