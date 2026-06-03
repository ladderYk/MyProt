using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyProtWeb.Services
{
    public interface IProtocolService
    {
        void Init(string protocolFolder);
        Task<IEnumerable<ProtocolConfig>> GetAllAsync();
        Task<ProtocolConfig?> GetByIdAsync(string id);
        Task<ProtocolConfig> CreateAsync(ProtocolConfig config);
        Task<bool> UpdateAsync(string id, ProtocolConfig config);
        Task<bool> DeleteAsync(string id);
    }
}
