using MyProt.Services;
using MyProt.webapi;

namespace MyProtWeb
{
    public class App
    {
        public static IProtocolService ProtocolService { get; } = new InMemoryProtocolService();
        public void Init()
        {
            ProtocolService.Init("./protocols");
            WebApiHost.Start(); // 启动带 Carter 的主机
        }
    }
}
