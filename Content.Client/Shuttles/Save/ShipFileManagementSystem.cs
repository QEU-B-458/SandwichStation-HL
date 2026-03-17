using Content.Shared.Shuttles.Save;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Log;
using Robust.Shared.ContentPack;

namespace Content.Client.Shuttles.Save
{
    public sealed class ShipFileManagementSystem : EntitySystem
    {
        [Dependency] private readonly IResourceManager _resourceManager = default!;
        private ISawmill _sawmill = default!;

        private static readonly Dictionary<string, string> CachedShipData = new();
        private static readonly Dictionary<string, (string shipName, DateTime timestamp)> ShipMetadataCache = new();
        private static readonly List<string> AvailableShips = new();
        private static event Action? ShipsUpdated;
        private static event Action<string>? ShipLoaded;
        private static bool _indexUpdateNeeded = false;
        private static DateTime _lastIndexUpdate = DateTime.MinValue;
        private static readonly TimeSpan IndexUpdateCooldown = TimeSpan.FromSeconds(1);

        public event Action? OnShipsUpdated
        {
            add => ShipsUpdated += value;
            remove => ShipsUpdated -= value;
        }

        public event Action<string>? OnShipLoaded
        {
            add => ShipLoaded += value;
            remove => ShipLoaded -= value;
        }

        private static int _instanceCounter = 0;
        private readonly int _instanceId;

        public ShipFileManagementSystem()
        {
            _instanceId = ++_instanceCounter;
        }

        public override void Initialize()
        {
            base.Initialize();
            _sawmill = Logger.GetSawmill("shipfile");
            SubscribeNetworkEvent<SendShipSaveDataClientMessage>(HandleSaveShipDataClient);
            SubscribeNetworkEvent<SendAvailableShipsMessage>(HandleAvailableShipsMessage);
            SubscribeNetworkEvent<ShipConvertedToSecureFormatMessage>(HandleShipConvertedToSecureFormat);
            SubscribeNetworkEvent<AdminRequestPlayerShipsMessage>(HandleAdminRequestPlayerShips);
            SubscribeNetworkEvent<AdminRequestShipDataMessage>(HandleAdminRequestShipData);
            SubscribeNetworkEvent<Content.Shared.Shuttles.Save.DeleteLocalShipFileMessage>(HandleDeleteLocalShipFile);

            EnsureSavedShipsDirectoryExists();

            if (AvailableShips.Count == 0)
            {
                LoadExistingShips();
            }

            RaiseNetworkEvent(new RequestAvailableShipsMessage());
        }

        private void EnsureSavedShipsDirectoryExists()
        {
            // Exports folder already exists, no need to create directories
        }

        private void HandleSaveShipDataClient(SendShipSaveDataClientMessage message)
        {
            // Save ship data to user data directory using sandbox-safe resource manager
            _sawmill.Info($"Client received ship save data for: {message.ShipName}");

            // Ensure directory exists before saving
            EnsureSavedShipsDirectoryExists();

            var fileName = $"/Exports/{message.ShipName}_{DateTime.Now:yyyyMMdd_HHmmss}.yml";

            try
            {
                // Hash is already embedded in YAML from server - just write raw data
                using var writer = _resourceManager.UserData.OpenWriteText(new(fileName));
                writer.Write(message.ShipData);
                _sawmill.Info($"Saved ship {message.ShipName} to user data: {fileName}");
            }
            catch (Exception ex)
            {
                _sawmill.Error($"Failed to save ship {message.ShipName}: {ex.Message}");
            }

            CachedShipData[fileName] = message.ShipData;
            if (!AvailableShips.Contains(fileName))
            {
                AvailableShips.Add(fileName);
            }

            _indexUpdateNeeded = true;
            ShipsUpdated?.Invoke();
        }

        private void HandleAvailableShipsMessage(SendAvailableShipsMessage message)
        {
            // Don't clear locally loaded ships - server message is for server-side ships only
            // The client handles local ship files independently
            _sawmill.Debug($"Instance #{_instanceId}: Received {message.ShipNames.Count} available ships from server (not clearing local ships)");
            _sawmill.Debug($"Instance #{_instanceId}: Current state before processing: {AvailableShips.Count} ships, {CachedShipData.Count} cached");

            // Only add server ships that aren't already in our local list
            foreach (var serverShip in message.ShipNames)
            {
                if (!AvailableShips.Contains(serverShip))
                {
                    AvailableShips.Add(serverShip);
                }
            }
            _sawmill.Info($"Instance #{_instanceId}: Final state: {AvailableShips.Count} ships");
        }

        private void HandleShipConvertedToSecureFormat(ShipConvertedToSecureFormatMessage message)
        {
            _sawmill.Warning($"Legacy ship '{message.ShipName}' was automatically converted to secure format by server");
            var originalFile = AvailableShips.FirstOrDefault(ship =>
                ship.Contains(message.ShipName) || CachedShipData.ContainsKey(ship) &&
                CachedShipData[ship].Contains($"shipName: {message.ShipName}"));

            if (originalFile != null)
            {
                try
                {
                    using var writer = _resourceManager.UserData.OpenWriteText(new(originalFile));
                    writer.Write(message.ConvertedYamlData);
                    CachedShipData[originalFile] = message.ConvertedYamlData;
                    _sawmill.Info($"Successfully overwrote legacy ship file '{originalFile}'");
                }
                catch (Exception ex)
                {
                    _sawmill.Error($"Failed to overwrite legacy ship file '{originalFile}': {ex.Message}");
                }
            }
            else
            {
                var fileName = $"/Exports/{message.ShipName}_converted_{DateTime.Now:yyyyMMdd_HHmmss}.yml";
                try
                {
                    using var writer = _resourceManager.UserData.OpenWriteText(new(fileName));
                    writer.Write(message.ConvertedYamlData);
                    CachedShipData[fileName] = message.ConvertedYamlData;
                    if (!AvailableShips.Contains(fileName)) AvailableShips.Add(fileName);
                    _sawmill.Debug($"Created new secure format file: {fileName}");
                }
                catch (Exception ex)
                {
                    _sawmill.Error($"Failed to create converted ship file: {ex.Message}");
                }
            }
        }

        public void RequestSaveShip(EntityUid deedUid)
        {
            RaiseNetworkEvent(new RequestSaveShipServerMessage((uint)deedUid.Id));
        }

        public async Task LoadShipFromFile(string filePath)
        {
            var yamlData = await GetShipYamlData(filePath);
            if (yamlData != null)
            {
                // Extract hash from YAML for validation
                var securityHash = ExtractSecurityHashFromYaml(yamlData);

                // Validate hash exists
                if (string.IsNullOrWhiteSpace(securityHash))
                {
                    _sawmill.Error($"Ship file '{filePath}' is missing security hash! This file may have been tampered with or is from an older version. Load rejected.");
                    return;
                }

                RaiseNetworkEvent(new RequestLoadShipMessage(yamlData, securityHash));
                var shipName = ExtractFileNameWithoutExtension(filePath);
                ShipLoaded?.Invoke(shipName);
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Extracts the security hash from the YAML metadata section.
        /// </summary>
        private string ExtractSecurityHashFromYaml(string yamlData)
        {
            var lines = yamlData.Split('\n');
            var inMetaSection = false;

            foreach (var line in lines)
            {
                if (line.StartsWith("meta:"))
                {
                    inMetaSection = true;
                    continue;
                }

                if (inMetaSection)
                {
                    if (!line.StartsWith("  ") && line.Trim().Length > 0)
                    {
                        break;
                    }

                    if (line.StartsWith("  securityHash:"))
                    {
                        var hashValue = line.Substring("  securityHash:".Length).Trim();
                        return hashValue;
                    }
                }
            }

            _sawmill.Warning("Security hash not found in ship YAML file");
            return string.Empty;
        }

        public async Task<string?> GetShipYamlData(string filePath)
        {
            if (CachedShipData.TryGetValue(filePath, out var yamlData))
                return yamlData;

            try
            {
                using var reader = _resourceManager.UserData.OpenText(new(filePath));
                yamlData = reader.ReadToEnd();
                CachedShipData[filePath] = yamlData;
            }
            catch (Exception ex)
            {
                _sawmill.Error($"Failed to load ship data from {filePath}: {ex.Message}");
                return null;
            }
            await Task.CompletedTask;
            return yamlData;
        }

        /// <summary>
        /// Reads ship YAML data directly from the uploaded file, bypassing the in-memory cache.
        /// Any mid-round changes are detected by server-side hash validation instead of using the cached hash.
        /// </summary>
        public async Task<string?> GetShipYamlDataFromDisk(string filePath)
        {
            try
            {
                using var reader = _resourceManager.UserData.OpenText(new(filePath));
                var content = reader.ReadToEnd();
                CachedShipData[filePath] = content;
                return content;
            }
            catch (Exception ex)
            {
                _sawmill.Error($"Failed to read ship file from disk {filePath}: {ex.Message}");
                return null;
            }
            await Task.CompletedTask;
        }

        private void LoadExistingShips()
        {
            try
            {
                var (ymlFiles, _) = _resourceManager.UserData.Find("*.yml", recursive: true);
                foreach (var file in ymlFiles)
                {
                    var filePath = file.ToString();

                    // Accept any .yml file in Exports (not just ship_index), but exclude backups
                    if (filePath.Contains("Exports")
                        && !filePath.Contains("Exports/backup")
                        && filePath.EndsWith(".yml")
                        && !filePath.Contains("ship_index"))
                    {
                        if (!AvailableShips.Contains(filePath))
                        {
                            AvailableShips.Add(filePath);

                            // Use lazy loading - only cache metadata for now
                            try
                            {
                                CacheShipMetadata(filePath);
                            }
                            catch (Exception shipEx)
                            {
                                _sawmill.Error($"Failed to cache metadata for {filePath}: {shipEx.Message}");
                            }
                        }
                    }
                }

                _sawmill.Debug($"Instance #{_instanceId}: Final result: Loaded {AvailableShips.Count} saved ships from Exports directory");

                // Trigger UI update
                ShipsUpdated?.Invoke();
            }
            catch (NotImplementedException) { }
            catch (Exception ex)
            {
                _sawmill.Error($"Failed to load existing ships: {ex.Message}");
            }
        }

        private void CacheShipMetadata(string filePath)
        {
            try
            {
                using var reader = _resourceManager.UserData.OpenText(new(filePath));
                var content = reader.ReadToEnd();
                var lines = content.Split('\n');
                var shipName = lines.FirstOrDefault(l => l.Trim().StartsWith("shipName:"))?.Split(':')[1].Trim() ?? "Unknown";
                var timestampStr = lines.FirstOrDefault(l => l.Trim().StartsWith("timestamp:"))?.Split(':', 2)[1].Trim() ?? "";
                if (DateTime.TryParse(timestampStr, out var timestamp))
                {
                    ShipMetadataCache[filePath] = (shipName, timestamp);
                }
            }
            catch (Exception ex)
            {
                _sawmill.Warning($"Failed to cache metadata for {filePath}: {ex.Message}");
            }
        }

        public void FlushPendingIndexUpdates()
        {
            if (_indexUpdateNeeded) UpdateShipIndex();
        }

        private void UpdateShipIndex()
        {
            try
            {
                var now = DateTime.Now;
                if (!_indexUpdateNeeded || (now - _lastIndexUpdate) < IndexUpdateCooldown) return;
                var indexContent = string.Join('\n', AvailableShips);
                using var writer = _resourceManager.UserData.OpenWriteText(new("/Exports/ship_index.txt"));
                writer.Write(indexContent);
                _indexUpdateNeeded = false;
                _lastIndexUpdate = now;
            }
            catch (Exception ex)
            {
                _sawmill.Error($"Failed to update ship index: {ex.Message}");
            }
        }

        public List<string> GetSavedShipFiles() => new List<string>(AvailableShips);
        public bool HasShipData(string shipName) => CachedShipData.ContainsKey(shipName);
        public string? GetShipData(string shipName) => CachedShipData.TryGetValue(shipName, out var data) ? data : null;

        private void HandleAdminRequestPlayerShips(AdminRequestPlayerShipsMessage message)
        {
            try
            {
                var playerManager = IoCManager.Resolve<Robust.Client.Player.IPlayerManager>();
                if (playerManager.LocalSession?.UserId != message.PlayerId) return;

                var ships = new List<(string filename, string shipName, DateTime timestamp)>();
                foreach (var filename in AvailableShips)
                {
                    if (ShipMetadataCache.TryGetValue(filename, out var metadata))
                        ships.Add((filename, metadata.shipName, metadata.timestamp));
                    else
                    {
                        try
                        {
                            CacheShipMetadata(filename);
                            if (ShipMetadataCache.TryGetValue(filename, out metadata))
                                ships.Add((filename, metadata.shipName, metadata.timestamp));
                        }
                        catch { }
                    }
                }
                RaiseNetworkEvent(new AdminSendPlayerShipsMessage(ships, message.AdminName));
            }
            catch (Exception ex)
            {
                _sawmill.Error($"Failed to handle admin request for player ships: {ex.Message}");
            }
        }

        private void HandleAdminRequestShipData(AdminRequestShipDataMessage message)
        {
            try
            {
                if (CachedShipData.TryGetValue(message.ShipFilename, out var shipData))
                {
                    RaiseNetworkEvent(new AdminSendShipDataMessage(shipData, message.ShipFilename, message.AdminName));
                }
            }
            catch (Exception ex)
            {
                _sawmill.Error($"Failed to handle admin request for ship data: {ex.Message}");
            }
        }

        private void HandleDeleteLocalShipFile(Content.Shared.Shuttles.Save.DeleteLocalShipFileMessage message)
        {
            try
            {
                var originalPath = new Robust.Shared.Utility.ResPath(message.FilePath);
                if (_resourceManager.UserData.Exists(originalPath))
                {
                    var backupDir = new Robust.Shared.Utility.ResPath("/Exports/backup");
                    _resourceManager.UserData.CreateDir(backupDir);
                    var fileName = ExtractFileNameWithoutExtension(message.FilePath);
                    var destBase = new Robust.Shared.Utility.ResPath($"/Exports/backup/{fileName}");
                    var destinationPath = new Robust.Shared.Utility.ResPath(destBase.ToString() + ".yml");

                    if (_resourceManager.UserData.Exists(destinationPath))
                    {
                        var timestamped = new Robust.Shared.Utility.ResPath($"/Exports/backup/{fileName}_loaded_{DateTime.Now:yyyyMMdd_HHmmss}.yml");
                        destinationPath = timestamped;
                    }

                    string fileContents;
                    using (var reader = _resourceManager.UserData.OpenText(originalPath))
                        fileContents = reader.ReadToEnd();

                    using (var writer = _resourceManager.UserData.OpenWriteText(destinationPath))
                        writer.Write(fileContents);

                    _resourceManager.UserData.Delete(originalPath);
                    _sawmill.Info($"Moved local ship file to backup: {message.FilePath} -> {destinationPath}");
                }
                CachedShipData.Remove(message.FilePath);
                ShipMetadataCache.Remove(message.FilePath);
                AvailableShips.Remove(message.FilePath);
                _indexUpdateNeeded = true;
                ShipsUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                _sawmill.Warning($"Failed to move local ship file '{message.FilePath}' to backup: {ex.Message}");
            }
        }

        private static string ExtractFileNameWithoutExtension(string filePath)
        {
            var fileName = filePath;
            var lastSlash = filePath.LastIndexOf('/');
            if (lastSlash >= 0) fileName = filePath.Substring(lastSlash + 1);
            var lastBackslash = fileName.LastIndexOf('\\');
            if (lastBackslash >= 0) fileName = fileName.Substring(lastBackslash + 1);
            var lastDot = fileName.LastIndexOf('.');
            if (lastDot >= 0) fileName = fileName.Substring(0, lastDot);
            return fileName;
        }
    }
}
