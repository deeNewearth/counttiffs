using BitMiracle.LibTiff.Classic;
using FolderProcessor;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CountTiffs
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        readonly ReadFolder<TiffInfo> _folderReader;

        public Worker(ReadFolder<TiffInfo> folderReader,
            ILogger<Worker> logger)
        {
            _logger = logger;
            _folderReader = folderReader;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var bacthSize = 5;

            _logger.LogInformation("Staring process");

            foreach (var fiBatch in MoreLinq.Extensions.BatchExtension.Batch(_folderReader.getFiles("*.tif"), bacthSize))
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Stop request received");
                    break;
                }

                await Task.WhenAll(fiBatch.Select(async fi =>
                {
                    await Task.Run(() =>
                    {
                        try
                        {
                            using (var image = Tiff.Open(fi.FullName, "r"))
                            {
                                _folderReader.updateStatus(fi, new TiffInfo
                                {
                                    pageCount = image.NumberOfDirectories(),
                                    size = fi.Length
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Failed processing file {fi.FullName} : {ex}");
                        }

                    });
                }));
                    
            }

            _logger.LogInformation("Completed processing. Calculating summery");

            var totalImages = _folderReader.status.Keys.Count();

            var totalPageCount = _folderReader.status.Values.Sum(v => v.pageCount);

            var totalsize = _folderReader.status.Values.Sum(v => v.size);

            _logger.LogInformation($"Found {totalImages} images -> {totalPageCount} pages -> Total size { BytesToString(totalsize)}");


            _logger.LogInformation("All done you can terimante the application");

        }

        static String BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }
    }

    public class TiffInfo
    {
        public int pageCount { get; set; }
        public long size { get; set; }
    }
}
