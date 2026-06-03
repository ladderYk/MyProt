using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyProtConsole
{
    public class ProtocolConfig
    {
        // 协议名称
        public string protocolName { get; set; }
        // 通讯类型
        public TransportConfig transport { get; set; }
        // 连接配置
        public ConnectionConfig connection { get; set; }
        // 协议列表
        public Dictionary<string, OperationConfig> operations { get; set; }
        // 构建方法
        public List<string> builtInFunctions { get; set; }
        // 握手协议
        public List<HandshakeConfig> handshake { get; set; }
        // 返回帧结构
        public FramingConfig framing { get; set; }
    }

    public class TransportConfig
    {
        public string type { get; set; }   // "Tcp", "Serial"
        public int defaultPort { get; set; }
    }
    public class ConnectionConfig
    {
        public int responseTimeoutMs { get; set; } = 1000;
        public int interFrameDelayMs { get; set; } = 0;
    }

    public class OperationConfig
    {
        public List<string> requestTemplate { get; set; }
        public ResponseParserConfig responseParser { get; set; }
    }

    public class ResponseParserConfig
    {
        public string validCondition { get; set; }
        public int dataStartIndex { get; set; }
        public string dataLengthExpr { get; set; }
        public string valueType { get; set; }   // "ByteArray", "UInt16", "Empty"
    }

    public class DeviceConfig
    {
        public string id { get; set; }
        public string protocol { get; set; }
        public string host { get; set; }
        public int port { get; set; }
    }

    public class TagDefinition
    {
        public string tagName { get; set; }
        public string deviceId { get; set; }
        public string operation { get; set; }
        public Dictionary<string, object> variables { get; set; }
        //public string FinalType { get; set; }   // "Float", "Bool", "UInt16", "String" ...
        public int scanRateMs { get; set; }
    }
    public class HandshakeConfig
    {
        public string name { get; set; }
        public List<string> requestTemplate { get; set; }
        public FramingConfig framing { get; set; }
        public string validCondition { get; set; }
    }
    public class FramingConfig
    {
        public string type { get; set; }                         // "LengthField", "Fixed"
        public int? lengthFieldOffset { get; set; }
        public int? lengthFieldLength { get; set; }
        public bool? lengthIncludesHeader { get; set; }
        public string byteOrder { get; set; }                    // "BigEndian", "LittleEndian"
        public int? headerLength { get; set; }                   // 仅当 !LengthIncludesHeader
        public int? fixedLength { get; set; }                    // Fixed 模式
    }
}
