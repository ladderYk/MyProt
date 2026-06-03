using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyProtConsole
{
    public class PollingEngine : BackgroundService
    {
        private readonly DeviceManager _deviceManager;
        private readonly List<TagDefinition> _tags;
        private readonly TagReader _tagReader;
        private readonly ILogger<PollingEngine> _logger;

        // 数据接收事件（可选，用于将数据传递给外部模块，如 MQTT 转发、数据库写入等）
        public event Action<TagValue>? OnDataReceived;

        public PollingEngine(
            DeviceManager deviceManager,
            List<TagDefinition> tags,
            TagReader tagReader,
            ILogger<PollingEngine> logger)
        {
            _deviceManager = deviceManager;
            _tags = tags;
            _tagReader = tagReader;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 等待所有设备连接完成
            await _deviceManager.ConnectAllAsync();

            // 按扫描周期分组（过滤掉 scanRateMs <= 0 的按需标签）
            var groups = _tags
                .Where(t => t.scanRateMs > 0)
                .GroupBy(t => t.scanRateMs)
                .ToList();

            if (groups.Count == 0)
            {
                //_logger.LogWarning("没有需要定时采集的标签");
                return;
            }

            // 为每个扫描周期组启动一个定时任务
            var tasks = groups.Select(group => PollGroupAsync(group.Key, group.ToList(), stoppingToken));
            await Task.WhenAll(tasks);
        }

        private async Task PollGroupAsync(int intervalMs, List<TagDefinition> tags, CancellationToken ct)
        {
            //_logger.LogInformation("启动采集组: 周期 {Interval}ms, 标签数 {Count}", intervalMs, tags.Count);

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));
            while (await timer.WaitForNextTickAsync(ct))
            {
                // 按设备分组，同一设备的请求串行，不同设备并发
                var deviceGroups = tags.GroupBy(t => t.deviceId);
                var tasks = deviceGroups.Select(async deviceGroup =>
                {
                    var channel = _deviceManager.GetChannel(deviceGroup.Key); // 内部已加锁

                    foreach (var tag in deviceGroup)
                    {
                        ct.ThrowIfCancellationRequested();

                        try
                        {
                            // 读取原始数据
                            var raw = await _tagReader.ReadRawAsync(tag.tagName);

                            var tagValue = new TagValue
                            {
                                TagName = tag.tagName,
                                RawData = raw,
                                Timestamp = DateTime.UtcNow,
                                Quality = QualityCode.Good
                            };

                            // 触发事件（传递给外部模块）
                            OnDataReceived?.Invoke(tagValue);

                            // 日志（可选）
                           // _logger.LogDebug("采集 {Tag} 成功, 数据长度 {Len}", tag.tagName, raw.Length);
                        }
                        catch (Exception ex)
                        {
                            var tagValue = new TagValue
                            {
                                TagName = tag.tagName,
                                Quality = QualityCode.Bad
                            };
                            OnDataReceived?.Invoke(tagValue);

                            //_logger.LogError(ex, "采集 {Tag} 失败", tag.tagName);
                        }
                    }
                });

                await Task.WhenAll(tasks);
            }
        }
    }

    // 标签值传输对象
    public class TagValue
    {
        public string TagName { get; set; } = string.Empty;
        public byte[]? RawData { get; set; }
        public DateTime Timestamp { get; set; }
        public QualityCode Quality { get; set; }
    }

    public enum QualityCode
    {
        Good = 0,
        Bad = 1
    }
}
