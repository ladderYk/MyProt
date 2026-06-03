using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyProtConsole
{
    public class TagReader
    {
        private readonly DeviceManager _deviceManager;      // 设备连接管理器
        private readonly Dictionary<string, TagDefinition> _tagDict;   // 所有标签

        public TagReader(DeviceManager deviceManager, List<TagDefinition> tags)
        {
            _deviceManager = deviceManager;
            _tagDict = tags.ToDictionary(t => t.tagName);
        }

        /// <summary>
        /// 读取单个标签的原始响应字节
        /// </summary>
        public async Task<byte[]> ReadRawAsync(string tagName)
        {
            if (!_tagDict.TryGetValue(tagName, out var tagDef))
                throw new ArgumentException($"标签 {tagName} 未定义");

            // 获取设备对应的协议和通道
            var device = _deviceManager.GetDevice(tagDef.deviceId);
            var protocol = _deviceManager.GetProtocolAsync(device.protocol);
            var channel = _deviceManager.GetChannel(device.id); // 内部含并发锁

            // 查找操作定义
            if (!protocol.operations.TryGetValue(tagDef.operation, out var opConfig))
                throw new InvalidOperationException($"操作 {tagDef.operation} 未在协议中定义");

            // 构建请求报文
            var request = ProtocolEngine.BuildRequest(opConfig.requestTemplate, tagDef.variables);

            // 发送并接收（通道内部保证串行）
            var response = await channel.SendReceiveAsync(request);

            return response;   // 返回完整原始字节
        }

        /// <summary>
        /// 读取标签并解析为最终值（需要配置 responseParser）
        /// </summary>
        public async Task<object> ReadValueAsync(string tagName)
        {
            var raw = await ReadRawAsync(tagName);
            var tagDef = _tagDict[tagName];
            var device = _deviceManager.GetDevice(tagDef.deviceId);
            var protocol = _deviceManager.GetProtocolAsync(device.protocol);
            var opConfig = protocol.operations[tagDef.operation];

            if (opConfig.responseParser == null)
                throw new NotSupportedException("该操作未配置响应解析器，无法自动转换值");

            return ProtocolEngine.ParseResponse(raw, opConfig.responseParser);
        }
    }
}
