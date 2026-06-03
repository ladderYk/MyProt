using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyProtConsole
{
    public class TcpChannel : IDisposable
    {
        private TcpClient _client; // 客户端
        private string _host;
        private int _port;
        private int _timeoutMs;
        private FramingConfig _framing;
        private NetworkStream _stream;

        public bool IsConnected => _client?.Connected ?? false;
        public TcpChannel(FramingConfig framing = null)
        {
            _framing = framing;
        }

        public async Task ConnectAsync(string host, int port, int timeoutMs)
        {
            _host = host;
            _port = port;
            _timeoutMs = timeoutMs;
            _client = new TcpClient();
            await _client.ConnectAsync(_host, _port);
            _stream = _client.GetStream();
            _stream.ReadTimeout = _timeoutMs;
            _stream.WriteTimeout = _timeoutMs;
        }

        public void SendAsync(byte[] request)
        {
            _stream.WriteAsync(request);
        }
        private readonly SemaphoreSlim _lock = new(1, 1);

        public async Task<byte[]> SendReceiveAsync(byte[] request, FramingConfig overrideFraming = null)
        {
            await _lock.WaitAsync();

            try
            {
                if (!IsConnected)
                    return new byte[0];
                await _stream.WriteAsync(request);
                var framing = overrideFraming ?? _framing;

                if (framing == null)
                    throw new InvalidOperationException("未提供帧解析配置");
                switch (framing.type)
                {
                    case "Fixed":
                        return await ReadFixedAsync(framing.fixedLength.Value);
                    case "LengthField":
                        return await ReadLengthFieldFrameAsync(framing);
                    default:
                        throw new NotSupportedException($"不支持的 Framing 类型: {framing.type}");
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        // 固定长度读取
        private async Task<byte[]> ReadFixedAsync(int length)
        {
            byte[] buf = new byte[length];
            await ReadExactAsync(buf, 0, length);
            return buf;
        }

        // 长度字段模式通用读取
        private async Task<byte[]> ReadLengthFieldFrameAsync(FramingConfig f)
        {
            // 先读入最少字节：至少包含长度字段的完整部分
            int minHeader = f.lengthFieldOffset.Value + f.lengthFieldLength.Value;
            byte[] header = new byte[minHeader];
            await ReadExactAsync(header, 0, minHeader);

            // 从 header 中提取长度值
            long lengthValue = 0;
            for (int i = 0; i < f.lengthFieldLength; i++)
            {
                int pos = f.lengthFieldOffset.Value + i;
                if (f.byteOrder == "BigEndian" || f.byteOrder == null)
                    lengthValue = (lengthValue << 8) | header[pos];
                else
                    lengthValue |= (long)header[pos] << (8 * i);
            }

            int totalLength;
            if (f.lengthIncludesHeader == true)
            {
                totalLength = (int)lengthValue;       // 长度值已包含整个帧
            }
            else
            {
                totalLength = f.headerLength.Value + (int)lengthValue; // 头固定长度 + 数据长度
            }

            // 若还没读够头部的剩余部分，继续读
            if (totalLength > minHeader)
            {
                byte[] full = new byte[totalLength];
                Buffer.BlockCopy(header, 0, full, 0, minHeader);
                await ReadExactAsync(full, minHeader, totalLength - minHeader);
                return full;
            }
            else
            {
                // 总长 <= minHeader（理论上不应发生），直接返回已读部分
                return header;
            }
        }

        private async Task ReadExactAsync(byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int read = await _stream.ReadAsync(buffer, offset, count);
                if (read == 0) throw new IOException("连接关闭");
                offset += read;
                count -= read;
            }

        }

        public void Disconnect()
        {
            _stream?.Close();
            _client?.Close();
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
