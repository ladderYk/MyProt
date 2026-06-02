using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MyProt
{
    public class ProtocolGateway
    {
        private readonly Dictionary<string, ProtocolConfig> _protocols = new Dictionary<string, ProtocolConfig>();
        private readonly Dictionary<string, DeviceConfig> _devices = new Dictionary<string, DeviceConfig>();
        private readonly List<TagDefinition> _tags;
        private readonly Dictionary<string, TcpChannel> _channels = new Dictionary<string, TcpChannel>();
        private Dictionary<string, Timer> _keepAliveTimers = new Dictionary<string, Timer>();

        public ProtocolGateway(string protocolFolder, string tagsFile)
        {
            // 加载所有协议 JSON
            foreach (var file in Directory.GetFiles(protocolFolder, "*.json"))
            {
                using (System.IO.StreamReader streamReader = File.OpenText(file))
                {
                    using (JsonTextReader reader = new JsonTextReader(streamReader))
                    {
                        var proto = ((JObject)JToken.ReadFrom(reader)).ToObject<ProtocolConfig>();

                        _protocols[proto.protocolName] = proto;
                    }
                }
            }

            using (System.IO.StreamReader streamReader = File.OpenText(tagsFile))
            {
                using (JsonTextReader reader = new JsonTextReader(streamReader))
                {
                    // 加载设备与标签
                    var root = ((JObject)JToken.ReadFrom(reader)).ToObject<ConfigRoot>();

                    foreach (var dev in root.Devices)
                        _devices[dev.id] = dev;
                    _tags = root.Tags;
                }
            }

        }
        public List<ProtocolConfig> getList()
        {
            return _protocols.Values.ToList();
        }
        public List<DeviceConfig> getDeviceList()
        {
            return _devices.Values.ToList();
        }
        public List<TagDefinition> getTagList()
        {
            return _tags.ToList();
        }
        public async Task<TagValue> ReadTagAsync(string tagName)
        {

            var tag = _tags.First(t => t.tagName == tagName);
            var device = _devices[tag.deviceId];
            var protocol = _protocols[device.protocol];          // 按协议名查询
            var operation = protocol.operations[tag.operation];

            // 获取或创建 TCP 通道
            var channel = await GetChannelAsync(device, protocol);

            Thread.Sleep(200);
            // 构建请求
            var request = ProtocolEngine.BuildRequest(operation.requestTemplate, tag.variables);
            //var request = ProtocolEngine.BuildRequest(operation.RequestTemplate, tag.Variables);
            // 发送并接收
            byte[] response = await channel.SendReceiveAsync(request);

            // 解析响应
            var rawData = ProtocolEngine.ParseResponse(response, operation.responseParser);

            // 转换为最终类型
            //object finalValue = ConvertToFinalType(rawData as byte[], tag.FinalType);

            return new TagValue
            {
                TagName = tag.tagName,
                Value = rawData,
                Timestamp = DateTime.UtcNow,
                Quality = QualityCode.Good
            };
        }

        private async Task<TcpChannel> GetChannelAsync(DeviceConfig device, ProtocolConfig protocol)
        {
            if (!_channels.ContainsKey(device.id))
            {
                var channel = new TcpChannel(protocol.framing);

                int port = device.port > 0 ? device.port : protocol.transport.defaultPort;
                channel.ConnectAsync(device.host, port, protocol.connection.responseTimeoutMs);
                // --- 新增：执行协议握手 ---
                if (protocol.handshake != null)
                {
                    foreach (var step in protocol.handshake)
                    {
                        byte[] req = ProtocolEngine.BuildRequest(step.requestTemplate, new Dictionary<string, object>());
                        byte[] resp = await channel.SendReceiveAsync(req, step.framing);
                        if (resp.Length == 0 || !Validate(step.validCondition, resp))
                            throw new Exception($"握手失败: {step.name}");
                    }
                }
                _channels[device.id] = channel;
            }

            return _channels[device.id];
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
        private object ConvertToFinalType(byte[] data, string finalType)
        {
            if (data == null || data.Length == 0) return null;

            switch (finalType.ToLower())
            {
                case "bool":
                    return (data[0] & 0x01) != 0;
                case "uint16":
                    return (ushort)((data[0] << 8) | data[1]);
                case "float":
                    // 需要 4 字节，大端顺序交换
                    if (data.Length >= 4)
                    {
                        byte[] floatBytes = new byte[4];
                        // Modbus 常用顺序：寄存器 1 低字在前或高字在前？这里假设标准顺序：大端字顺序
                        Buffer.BlockCopy(data, 0, floatBytes, 0, 4);
                        if (BitConverter.IsLittleEndian) Array.Reverse(floatBytes);
                        return BitConverter.ToSingle(floatBytes, 0);
                    }
                    break;
            }
            return data;
        }
    }

    // 辅助类型
    public class ConfigRoot
    {
        public List<DeviceConfig> Devices { get; set; }
        public List<TagDefinition> Tags { get; set; }
    }

    public class TagValue
    {
        public string TagName { get; set; }
        public object Value { get; set; }
        public DateTime Timestamp { get; set; }
        public QualityCode Quality { get; set; }
    }

    public enum QualityCode { Good = 0, Bad = 1 }
}
