using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MyProt
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static ProtocolGateway gateway;
        public static DeviceManager dm;
        public MainWindow()
        {
            InitializeComponent();
            gateway = new ProtocolGateway("./Protocols", "./tags.json");
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            getDataAsync();
        }
        private async Task getDataAsync()
        {
            try
            {
                dm = new DeviceManager(gateway.getDeviceList(), (await App.ProtocolService.GetAllAsync()).ToList());
                await dm.ConnectAllAsync();
                //TagValue temp = await gateway.ReadTagAsync("DB1_Real0");
                //Console.WriteLine($"温度: {temp.Value}");
            }
            catch (Exception e)
            {
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            GetTaskAsync();

        }
        private async Task GetTaskAsync()
        {
            foreach (var tag in gateway.getTagList())
            {
                var channel = dm.GetChannel(tag.deviceId);
                var device = dm.GetDevice(tag.deviceId);
                var protocol = await App.ProtocolService.GetByIdAsync(device.protocol);          // 按协议名查询
                var operation = protocol.operations[tag.operation];
                var request = ProtocolEngine.BuildRequest(operation.requestTemplate, tag.variables);
                //var request = ProtocolEngine.BuildRequest(operation.RequestTemplate, tag.Variables);
                // 发送并接收
                byte[] response = await channel.SendReceiveAsync(request);

                // 解析响应
                var rawData = ProtocolEngine.ParseResponse(response, operation.responseParser);

                // 转换为最终类型
                //object finalValue = ConvertToFinalType(rawData as byte[], tag.FinalType);

                TagValue tagValue = new TagValue
                {
                    TagName = tag.tagName,
                    Value = rawData,
                    Timestamp = DateTime.UtcNow,
                    Quality = QualityCode.Good
                };
            }
        }
    }
}