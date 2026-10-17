using OrderProcessing.Shared.Exceptions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace OrderProcessing.Shared.Storage
{
    public class JsonFileStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        private static readonly ConcurrentDictionary<string, SemaphoreSlim>
            FileLocks = new(StringComparer.OrdinalIgnoreCase);

        private readonly string _dataDirectory;

        public JsonFileStore()
        {
            _dataDirectory = DataFiles.Root;
        }

        private string GetPath(string fileName)
        {
            return Path.Combine(_dataDirectory, fileName);
        }

        private SemaphoreSlim GetFileLock(string path)
        {
            return FileLocks.GetOrAdd(
                path,
                _ => new SemaphoreSlim(1, 1));
        }

        public async Task<List<T>> ReadAsync<T>(
            string fileName,
            CancellationToken ct)
        {
            string path = GetPath(fileName);
            SemaphoreSlim fileLock = GetFileLock(path);

            await fileLock.WaitAsync(ct);

            try
            {
                return await ReadUnlockedAsync<T>(path, ct);
            }
            finally
            {
                fileLock.Release();
            }
        }

        private async Task<List<T>> ReadUnlockedAsync<T>(
            string path,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (!File.Exists(path))
            {
                return new List<T>();
            }

            try
            {
                await using FileStream stream = new(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    FileOptions.Asynchronous);

                List<T>? records =
                    await JsonSerializer.DeserializeAsync<List<T>>(
                        stream,
                        JsonOptions,
                        ct);

                return records ?? new List<T>();
            }
            catch (JsonException ex)
            {
                throw new DataFileCorruptedException(Path.GetFileName(path), ex);
            }
        }

        public async Task WriteAsync<T>(
            string fileName,
            List<T> records,
            CancellationToken ct)
        {
            string path = GetPath(fileName);
            SemaphoreSlim fileLock = GetFileLock(path);

            await fileLock.WaitAsync(ct);

            try
            {
                await WriteUnlockedAsync(path, records, ct);
            }
            finally
            {
                fileLock.Release();
            }
        }

        private async Task WriteUnlockedAsync<T>(
            string path,
            List<T> records,
            CancellationToken ct)
        {
            Directory.CreateDirectory(_dataDirectory);

            string tempPath = path + "." +
                Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                await using (FileStream stream = new(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.Asynchronous))
                {
                    await JsonSerializer.SerializeAsync(
                        stream,
                        records,
                        JsonOptions,
                        ct);

                    await stream.FlushAsync(ct);
                }

                ct.ThrowIfCancellationRequested();

                File.Move(tempPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        public async Task UpdateAsync<T>(
            string fileName,
            Func<List<T>, List<T>> update,
            CancellationToken ct)
        {
            string path = GetPath(fileName);
            SemaphoreSlim fileLock = GetFileLock(path);

            await fileLock.WaitAsync(ct);

            try
            {
                List<T> records =
                    await ReadUnlockedAsync<T>(path, ct);

                ct.ThrowIfCancellationRequested();

                List<T> updatedRecords = update(records);

                ct.ThrowIfCancellationRequested();

                await WriteUnlockedAsync(path, updatedRecords, ct);
            }
            finally
            {
                fileLock.Release();
            }
        }

        public List<T> Read<T>(string fileName)
        {
            string path = GetPath(fileName);
            SemaphoreSlim fileLock = GetFileLock(path);

            fileLock.Wait();

            try
            {
                if (!File.Exists(path))
                {
                    return new List<T>();
                }

                try
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);

                    return JsonSerializer.Deserialize<List<T>>(
                        json,
                        JsonOptions
                    ) ?? new List<T>();
                }
                catch (JsonException ex)
                {
                    throw new DataFileCorruptedException(
                        Path.GetFileName(path),
                        ex
                    );
                }
            }
            finally
            {
                fileLock.Release();
            }
        }

        public void Write<T>(string fileName, List<T> records)
        {
            string path = GetPath(fileName);
            SemaphoreSlim fileLock = GetFileLock(path);

            fileLock.Wait();

            try
            {
                Directory.CreateDirectory(_dataDirectory);

                string tempPath = path + "." +
                    Guid.NewGuid().ToString("N") + ".tmp";

                try
                {
                    string json = JsonSerializer.Serialize(
                        records,
                        JsonOptions
                    );

                    File.WriteAllText(tempPath, json, Encoding.UTF8);

                    File.Move(tempPath, path, overwrite: true);
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
            }
            finally
            {
                fileLock.Release();
            }
        }

    }
}
