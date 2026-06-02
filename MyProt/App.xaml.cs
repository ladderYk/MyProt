using Microsoft.AspNetCore.Builder;
using MyProt.Services;
using MyProt.webapi;
using System.Configuration;
using System.Data;
using System.Windows;

namespace MyProt
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IProtocolService ProtocolService { get; } = new InMemoryProtocolService();
        //public static IDeviceService DeviceService { get; } = new InMemoryDeviceService();
        //public static ITagService TagService { get; } = new InMemoryTagService();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ProtocolService.Init("./Protocols");
            WebApiHost.Start(); // 启动带 Carter 的主机
        }

        protected override void OnExit(ExitEventArgs e)
        {
            //WebApiHost.StopAsync().Wait();
            base.OnExit(e);
        }
    }

}
