using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FolderProcessor
{
    public class ReadFolder { }

    /// <summary>
    /// This class Has functionality to enumurate all "files" in a folder that needs processing
    /// </summary>
    public class ReadFolder<T>: ReadFolder
    {
        readonly ILogger _logger;
        readonly FolderConfig _config;

        

        readonly ConcurrentDictionary<string, T> _statusMap;
        readonly StreamWriter _statusWriter;

        public ReadFolder(IConfiguration config,
            ILogger<ReadFolder> logger
            )
        {
            _logger = logger;
            _config = config.GetSection("folderConfig").Get<FolderConfig>();

            _logger.LogInformation($"using imageroot {_config.imageRoot}");

            if (string.IsNullOrWhiteSpace(_config?.imageRoot))
                throw new Exception("config imageRoot is null");

            if (!Directory.Exists(_config.imageRoot))
            {
                throw new Exception($"imageRoot folder {_config.imageRoot} does not Exist");
            }

            var statusPath = string.IsNullOrWhiteSpace(_config?.statusFilePath) ? _config.imageRoot : _config?.statusFilePath;

            var statusFile = Path.Combine(statusPath, _config.statusFileName);

            _logger.LogInformation($"using statusFile {statusFile}");

            if (!Directory.Exists(statusPath))
            {
                Directory.CreateDirectory(statusPath);
            }

            _statusMap =  new ConcurrentDictionary<string, T>(  LoadStatusFile(statusFile));

            _statusWriter = File.AppendText(statusFile);

        }

        public IReadOnlyDictionary<string, T> status => _statusMap;

        class StatusFileRecord { 
            public string filePath { get; set; }
            public T status { get; set; }
        }

        IEnumerable<StatusFileRecord> readCacheFile(string filename)
        {
            Console.WriteLine($"loading status file {filename}");

            long errorCount = 0;

            using (var file = new System.IO.StreamReader(filename))
            {
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    StatusFileRecord val;
                    try
                    {
                        val = JsonConvert.DeserializeObject<StatusFileRecord>(line);
                    }
                    catch (Exception ex)
                    {
                        var g = line.Split('\n');

                        ++errorCount;

                        _logger.LogDebug ($"json error errorCount -> {++errorCount}, ex: {ex}");
                        continue;

                        //throw ex;
                    }
                    yield return val;
                }

                if(errorCount > 0)
                {
                    _logger.LogWarning($"json error errorCount -> {errorCount}");
                }
                

                file.Close();
            }
        }

        Dictionary<string,T> LoadStatusFile(string statusFile)
        {
            _logger.LogDebug($"Loading status file {statusFile}");
            if (!File.Exists(statusFile))
            {
                _logger.LogInformation($"Status file is not there starting from Scratch");
                return new Dictionary<string, T>();
            }

            var ret = new Dictionary<string, T>();
            foreach (var kv in readCacheFile(statusFile))
            {
                if (null == kv)
                    continue;

                if (!ret.ContainsKey(kv.filePath))
                {
                    ret[kv.filePath] = kv.status;
                }
            }

            _logger.LogInformation($"Statusfile loaded with {ret.Count()} records");

            return ret;

        }

        static readonly object _fileWriteLock = new object();

        public void updateStatus(FileInfo fi, T status)
        {
            var jSonData = JsonConvert.SerializeObject(new StatusFileRecord
            {
                filePath = fi.FullName,
                status = status
            }, Formatting.None);
            

            lock (_fileWriteLock)
            {
                _statusWriter.WriteLine(jSonData);

                _statusWriter.Flush();
            }

            _statusMap[fi.FullName] = status;
        }

        public IEnumerable<FileInfo> getFiles(string pattern = "*.*")
        {
            
            var dir = new DirectoryInfo(_config.imageRoot);

            _logger.LogDebug($"Enumurating with pattern {pattern}");

            var allFileInfos = dir.EnumerateFiles(pattern, System.IO.SearchOption.AllDirectories);

            long skipCount = 0;
            long doneCount = 0;

            foreach (var fi in allFileInfos)
            {
                if (_statusMap.ContainsKey(fi.FullName))
                {
                    _logger.LogTrace($"{fi.FullName} is marked done in status file");

                    Interlocked.Increment(ref skipCount);

                    if (1 == skipCount % 100)
                    {
                        _logger.LogInformation($"Current skipCount: {skipCount} ->{fi.FullName}");
                    }
                    continue;
                }
                yield return fi;

                Interlocked.Increment(ref doneCount);

                if (1 == doneCount % 100)
                {
                    _logger.LogInformation($"Current donecount: {doneCount} ->{fi.FullName}");
                }
            }

            _logger.LogInformation($"Completed all {skipCount + doneCount} files");
        }

    }

    /// <summary>
    /// The configurations for this
    /// </summary>
    public class FolderConfig
    {
        /// <summary>
        /// The folder we are reading
        /// </summary>
        public string imageRoot { get; set; }

        /// <summary>
        /// Optional
        /// </summary>
        public string statusFileName { get; set; } = @"folderprocessor.status";

        /// <summary>
        /// Optional: we use imageRoot if null
        /// </summary>
        public string statusFilePath { get; set; }
    }
}
