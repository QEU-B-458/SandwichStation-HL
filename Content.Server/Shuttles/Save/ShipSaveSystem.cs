using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Shared.Shuttles.Save;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.CCVar;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Robust.Shared.Log;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server.Shuttles.Save
{
    public sealed class ShipSaveSystem : EntitySystem
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;

        private static readonly Dictionary<string, Action<string>> PendingAdminRequests = new();
        private static readonly Dictionary<string, List<(string filename, string shipName, DateTime timestamp)>> PlayerShipCache = new();
        private ISawmill _sawmill = default!;

        public override void Initialize()
        {
            base.Initialize();
            _sawmill = Logger.GetSawmill("shipyard.shipsave");
            _sawmill.Info("ShipSaveSystem initializing - subscribing to network events...");

            SubscribeNetworkEvent<RequestSaveShipServerMessage>(OnRequestSaveShipServer);
            SubscribeNetworkEvent<RequestLoadShipMessage>(OnRequestLoadShip);
            SubscribeNetworkEvent<RequestAvailableShipsMessage>(OnRequestAvailableShips);
            SubscribeNetworkEvent<AdminSendPlayerShipsMessage>(OnAdminSendPlayerShips);
            SubscribeNetworkEvent<AdminSendShipDataMessage>(OnAdminSendShipData);

            _sawmill.Info("ShipSaveSystem initialized - all event subscriptions complete");
        }

        private void OnRequestSaveShipServer(RequestSaveShipServerMessage msg, EntitySessionEventArgs args)
        {
            var playerSession = args.SenderSession;
            if (playerSession == null) return;

            var deedUid = new EntityUid((int)msg.DeedUid);
            if (!_entityManager.TryGetComponent<ShuttleDeedComponent>(deedUid, out var deed) || deed.ShuttleUid == null ||
                !_entityManager.TryGetEntity(deed.ShuttleUid.Value, out var shuttleNetUid))
            {
                _sawmill.Warning($"Player {playerSession.Name} attempted ship save without a valid shuttle deed");
                return;
            }

            var gridToSave = shuttleNetUid.Value;
            if (!_entityManager.HasComponent<MapGridComponent>(gridToSave))
            {
                _sawmill.Warning($"Player {playerSession.Name} deed shuttle {gridToSave} is not a grid");
                return;
            }

            var shipName = deed.ShuttleName ?? $"SavedShip_{DateTime.Now:yyyyMMdd_HHmmss}";
            var shipyardGridSaveSystem = _entitySystemManager.GetEntitySystem<Content.Server._NF.Shipyard.Systems.ShipyardGridSaveSystem>();

            // The hash generation is now handled inside ShipyardGridSaveSystem.TrySaveGridAsShip
            // to ensure the hash covers the actual YAML content.
            // We pass the player ID so the save system can include it in the HMAC if desired,
            // though the primary defense is the content hash.
            var success = shipyardGridSaveSystem.TrySaveGridAsShip(gridToSave, shipName, playerSession.UserId.ToString(), playerSession);

            if (success)
            {
                _sawmill.Info($"Successfully saved ship {shipName}");
                if (_entityManager.TryGetComponent<ShuttleDeedComponent>(deedUid, out var deedComp))
                    _entityManager.RemoveComponent<ShuttleDeedComponent>(deedUid);

                var toRemove = new List<EntityUid>();
                var query = _entityManager.EntityQueryEnumerator<ShuttleDeedComponent>();
                while (query.MoveNext(out var ent, out var deedRef))
                {
                    if (deedRef.ShuttleUid != null && _entityManager.TryGetEntity(deedRef.ShuttleUid.Value, out var entUid) && entUid == gridToSave)
                        toRemove.Add(ent);
                }
                foreach (var uidToClear in toRemove)
                    _entityManager.RemoveComponent<ShuttleDeedComponent>(uidToClear);

                _entityManager.QueueDeleteEntity(gridToSave);
            }
            else
            {
                _sawmill.Error($"Failed to save ship {shipName}");
            }
        }

        public void RequestSaveShip(EntityUid deedUid, ICommonSession? playerSession)
        {
            if (playerSession == null) return;
            if (!_entityManager.TryGetComponent<ShuttleDeedComponent>(deedUid, out var deedComponent)) return;
            if (deedComponent.ShuttleUid == null || !_entityManager.TryGetEntity(deedComponent.ShuttleUid.Value, out var shuttleUid) || !_entityManager.TryGetComponent<MapGridComponent>(shuttleUid.Value, out var grid)) return;

            var shipName = deedComponent.ShuttleName ?? "SavedShip_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var shipyardGridSaveSystem = _entitySystemManager.GetEntitySystem<Content.Server._NF.Shipyard.Systems.ShipyardGridSaveSystem>();

            // Hash generation moved to the save system to cover content
            var success2 = shipyardGridSaveSystem.TrySaveGridAsShip(shuttleUid.Value, shipName, playerSession.UserId.ToString(), playerSession);
            if (success2)
            {
                _entityManager.RemoveComponent<ShuttleDeedComponent>(deedUid);
                _sawmill.Info($"Successfully saved and removed ship {shipName}");
            }
            else
            {
                _sawmill.Error($"Failed to save ship {shipName}");
            }
        }

        private void OnRequestLoadShip(RequestLoadShipMessage msg, EntitySessionEventArgs args)
        {
            var playerSession = args.SenderSession;
            if (playerSession == null) 
            {
                _sawmill.Error("OnRequestLoadShip called but playerSession is null!");
                return;
            }

            _sawmill.Info($"Player {playerSession.Name} requested to load ship from YAML data");
            _sawmill.Info($"[SECURITY] VALIDATION TRIGGERED - YAML data received, starting hash validation...");

            // 1. Extract the hash embedded in the YAML string
            var extractedHash = ExtractHashFromYaml(msg.YamlData);
            _sawmill.Debug($"[SECURITY] Extracted hash from file: {extractedHash}");
            _sawmill.Debug($"[SECURITY] Hash from message: {msg.SecurityHash}");

            // 2. Check if hash is missing (file was never properly saved or is from old version)
            if (string.IsNullOrWhiteSpace(extractedHash))
            {
                _sawmill.Error($"[SECURITY] Ship load REJECTED for {playerSession.Name}: No security hash found in YAML. This file may have been tampered with or is from an unsupported version.");
                return; // Abort load
            }

            // 3. Recalculate hash to detect content tampering
            var serverSecret = _configurationManager.GetCVar(CCVars.UniqueServerHash);
            var cleanData = RemoveHashFieldFromYaml(msg.YamlData);

            _sawmill.Debug($"[SECURITY] Clean YAML length: {cleanData.Length} bytes (original: {msg.YamlData.Length} bytes)");
            _sawmill.Debug($"[SECURITY] Bytes removed: {msg.YamlData.Length - cleanData.Length}");

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(serverSecret));
            var recalculatedHash = BitConverter.ToString(
                hmac.ComputeHash(Encoding.UTF8.GetBytes(cleanData))
            ).Replace("-", "").ToLowerInvariant();

            _sawmill.Debug($"[SECURITY] Extracted hash from file: {extractedHash}");
            _sawmill.Debug($"[SECURITY] Recalculated hash:         {recalculatedHash}");
            _sawmill.Debug($"[SECURITY] Hashes match: {extractedHash.Equals(recalculatedHash, StringComparison.OrdinalIgnoreCase)}");

            // 4. The CRITICAL check: extracted hash must match recalculated hash
            // If they don't match, the file has been tampered with
            if (!extractedHash.Equals(recalculatedHash, StringComparison.OrdinalIgnoreCase))
            {
                _sawmill.Error($"[SECURITY] Ship load REJECTED for {playerSession.Name}: File has been tampered with or corrupted!");
                _sawmill.Error($"[SECURITY]   Expected hash (from server calculation): {recalculatedHash}");
                _sawmill.Error($"[SECURITY]   Found hash (in file): {extractedHash}");
                return; // Abort load
            }

            _sawmill.Info($"[SECURITY] ✓ Ship hash VALIDATED for player {playerSession.Name} - proceeding with load");

            try
            {
                var mapLoader = _entitySystemManager.GetEntitySystem<MapLoaderSystem>();
                var shipData = msg.YamlData;
                var tempFileName = $"/tmp/ship_load_{Guid.NewGuid()}.yml";

                try
                {
                    var resourceManager = IoCManager.Resolve<IResourceManager>();
                    using (var writer = resourceManager.UserData.OpenWriteText(new ResPath(tempFileName)))
                    {
                        writer.Write(shipData);
                    }

                    var success = mapLoader.TryLoadGeneric(new ResPath(tempFileName), out var maps, out var grids);
                    if (!success || maps == null || maps.Count == 0)
                    {
                        _sawmill.Error($"Failed to deserialize ship YAML for player {playerSession.Name}");
                        return;
                    }
                    _sawmill.Info($"Successfully loaded ship with {maps.Count} maps for player {playerSession.Name}");
                }
                catch (Exception loadEx)
                {
                    _sawmill.Error($"Exception while loading ship YAML: {loadEx.Message}");
                    throw;
                }
                finally
                {
                    try
                    {
                        var resourceManager = IoCManager.Resolve<IResourceManager>();
                        if (resourceManager.UserData.Exists(new ResPath(tempFileName)))
                            resourceManager.UserData.Delete(new ResPath(tempFileName));
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _sawmill.Error($"Failed to load ship: {ex.Message}");
            }
        }

        private void OnRequestAvailableShips(RequestAvailableShipsMessage msg, EntitySessionEventArgs args)
        {
            // Client handles this locally
        }

        private void OnAdminSendPlayerShips(AdminSendPlayerShipsMessage msg, EntitySessionEventArgs args)
        {
            var key = $"player_ships_{msg.AdminName}";
            if (PendingAdminRequests.TryGetValue(key, out var callback))
            {
                PlayerShipCache[key] = msg.Ships;
                var result = $"=== Ships for player ===\n\n";
                for (int i = 0; i < msg.Ships.Count; i++)
                {
                    var (filename, shipName, timestamp) = msg.Ships[i];
                    result += $"[{i + 1}] {shipName} ({filename})\n    Saved: {timestamp:yyyy-MM-dd HH:mm:ss}\n\n";
                }
                callback(result);
                PendingAdminRequests.Remove(key);
            }
        }

        private void OnAdminSendShipData(AdminSendShipDataMessage msg, EntitySessionEventArgs args)
        {
            var key = $"ship_data_{msg.AdminName}_{msg.ShipFilename}";
            if (PendingAdminRequests.TryGetValue(key, out var callback))
            {
                callback(msg.ShipData);
                PendingAdminRequests.Remove(key);
            }
        }

        public static void RegisterAdminRequest(string key, Action<string> callback)
        {
            PendingAdminRequests[key] = callback;
        }

        public void SendAdminRequestPlayerShips(Guid playerId, string adminName, ICommonSession targetSession)
        {
            RaiseNetworkEvent(new AdminRequestPlayerShipsMessage(playerId, adminName), targetSession);
        }

        public void SendAdminRequestShipData(string filename, string adminName, ICommonSession targetSession)
        {
            RaiseNetworkEvent(new AdminRequestShipDataMessage(filename, adminName), targetSession);
        }

        /// <summary>
        /// Extracts the security hash from the YAML string.
        /// </summary>
        private string ExtractHashFromYaml(string yamlData)
        {
            var match = Regex.Match(yamlData, @"securityHash:\s*""?([a-fA-F0-9]+)""?", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : string.Empty;
        }

        /// <summary>
        /// Removes the securityHash field from the YAML string to allow recalculation.
        /// Handles all newline styles: Windows (\r\n), Unix (\n), and old Mac (\r)
        /// </summary>
        private string RemoveHashFieldFromYaml(string yamlData)
        {
            // Remove the entire line containing securityHash with proper newline handling
            // This regex explicitly handles:
            // - Optional leading whitespace (^\s*)
            // - The securityHash key and value
            // - Any trailing whitespace
            // - All newline styles: \r\n (Windows), \n (Unix), \r (old Mac)
            return Regex.Replace(yamlData, 
                @"^\s*securityHash:\s*[a-fA-F0-9]+\s*(?:\r\n|\r|\n)?", 
                "", 
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
        }
    }
}
