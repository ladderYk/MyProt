using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;

namespace MyProt
{
    public class DeviceManager
    {
        private readonly Dictionary<string, DeviceConfig> _deviceConfigs;
        private readonly Dictionary<string, ProtocolConfig> _protocolConfigs;
        private readonly ConcurrentDictionary<string, TcpChannel> _channels = new();

        public DeviceManager(IEnumerable<DeviceConfig> devices, IEnumerable<ProtocolConfig> protocols)
        {
            _deviceConfigs = devices.ToDictionary(d => d.id);
            _protocolConfigs = protocols.ToDictionary(p => p.protocolName);
        }

        /// <summary>
        /// 启动时调用：为所有设备建立连接
        /// </summary>
        public async Task ConnectAllAsync()
        {
            var tasks = new List<Task>();
            foreach (var device in _deviceConfigs.Values)
            {
                tasks.Add(ConnectDeviceAsync(device));
            }
            await Task.WhenAll(tasks);
            Console.WriteLine($"已连接 {_channels.Count}/{_deviceConfigs.Count} 个设备");
        }

        private async Task ConnectDeviceAsync(DeviceConfig device)
        {
            if (!_protocolConfigs.TryGetValue(device.protocol, out var proto))
                throw new Exception($"协议 '{device.protocol}' 未定义");

            try
            {
                var channel = CreateChannel(proto.framing);
                await channel.ConnectAsync(device.host, device.port, proto.connection.responseTimeoutMs);

                // 执行握手（如果协议定义了）
                if (proto.handshake != null)
                {
                    foreach (var step in proto.handshake)
                    {
                        byte[] req = ProtocolEngine.BuildRequest(step.requestTemplate, new Dictionary<string, object>());
                        var resp = await channel.SendReceiveAsync(req, step.framing ?? proto.framing);
                        if (!Validate(step.validCondition, resp))
                            throw new Exception($"握手失败: {step.name}");
                    }
                }

                _channels[device.id] = channel;
                Console.WriteLine($"设备 {device.id} 连接成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"设备 {device.id} 连接失败: {ex.Message}");
                // 可选择在此启动后台重连定时器
            }
        }
        public TcpChannel CreateChannel(FramingConfig framing)
        {
            var channel = new TcpChannel(framing);
            return channel;
        }
        /// <summary>
        /// 获取设备通道（用于后续读写）
        /// </summary>
        public TcpChannel GetChannel(string deviceId)
        {
            if (_channels.TryGetValue(deviceId, out var ch) && ch.IsConnected)
                return ch;
            throw new Exception($"设备 {deviceId} 未连接");
        }
        public static bool Validate(string condition, byte[] resp)
        {
            if (string.IsNullOrEmpty(condition)) return true;
            var match = Regex.Match(condition, @"resp\[(\d+)\]\s*==\s*(0x[0-9A-Fa-f]+|\d+)");
            if (match.Success)
            {
                int idx = int.Parse(match.Groups[1].Value);
                byte expected = match.Groups[2].Value.StartsWith("0x") ?
                    Convert.ToByte(match.Groups[2].Value.Substring(2), 16) :
                    byte.Parse(match.Groups[2].Value);
                return resp[idx] == expected;
            }
            return true;
        }
    }
}
