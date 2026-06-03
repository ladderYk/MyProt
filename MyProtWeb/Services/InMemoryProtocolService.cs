using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyProtWeb.Services
{
    public class InMemoryProtocolService : IProtocolService
    {
        private readonly ConcurrentDictionary<string, ProtocolConfig> _protocols = new();
        public void Init(string protocolFolder)
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
        }
        public Task<IEnumerable<ProtocolConfig>> GetAllAsync()
            => Task.FromResult(_protocols.Values.AsEnumerable());

        public Task<ProtocolConfig?> GetByIdAsync(string id)
        {
            _protocols.TryGetValue(id, out var config);
            return Task.FromResult(config);
        }

        public Task<ProtocolConfig> CreateAsync(ProtocolConfig config)
        {
            _protocols.TryAdd(config.protocolName, config);
            return Task.FromResult(config);
        }

        public Task<bool> UpdateAsync(string id, ProtocolConfig config)
        {
            if (!_protocols.ContainsKey(id)) return Task.FromResult(false);
            _protocols[id] = config;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(string id)
            => Task.FromResult(_protocols.TryRemove(id, out _));
    }
}
