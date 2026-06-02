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
                TagValue temp = await gateway.ReadTagAsync("DB1_Real0");
                Console.WriteLine($"温度: {temp.Value}");
            }
            catch (Exception e)
            {
            }
        }
    }
    }