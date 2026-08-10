#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Junkinnering.Editor
{
    /// <summary>
    /// One-shot maintenance tool: moves every workshop part entry into a remote-hosted group so
    /// the part art is downloaded from the CDN instead of shipping inside the player. Idempotent —
    /// running it twice moves nothing the second time. Delete this file once the move has landed.
    ///
    /// The placeholder sprite and the tap game's TargetObject / FallbackTexture deliberately stay
    /// local: they are what the app falls back to when a remote load fails, so hosting them
    /// remotely would mean the failure path needs the network it is compensating for.
    /// </summary>
    public static class MoveWorkshopArtToRemote
    {
        private const string GroupName = "Remote Parts";
        private const int TimeoutSeconds = 8;
        private const int RetryCount = 1;

        // Matches the addresses the catalog uses, e.g. "head_crt.icon" / "weapon_sledge.full".
        private static readonly Regex PartAddress =
            new Regex(@"^(head|torso|arms|legs|weapon)_[a-z]+\.(icon|full)$");

        [MenuItem("Tools/Addressables/Move Workshop Art To Remote")]
        public static void Move()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError($"{nameof(MoveWorkshopArtToRemote)}.{nameof(Move)} no Addressables settings asset found");
                return;
            }

            AddressableAssetGroup group = settings.FindGroup(GroupName);
            if (group == null)
            {
                group = settings.CreateGroup(GroupName, false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                Debug.Log($"{nameof(MoveWorkshopArtToRemote)}.{nameof(Move)} created group '{GroupName}'");
            }

            BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
            {
                schema = group.AddSchema<BundledAssetGroupSchema>();
            }

            if (!schema.BuildPath.SetVariableByName(settings, "Remote.BuildPath") ||
                !schema.LoadPath.SetVariableByName(settings, "Remote.LoadPath"))
            {
                Debug.LogError($"{nameof(MoveWorkshopArtToRemote)}.{nameof(Move)} could not bind the Remote profile variables — check the profile names");
                return;
            }

            // The stock Remote Images group ships Timeout 0 / RetryCount 0, which means a stalled
            // request never completes and never fails: the load sits pending forever and its handle
            // stays live. Give this group real bounds so a stall surfaces as an ordinary failure.
            schema.Timeout = TimeoutSeconds;
            schema.RetryCount = RetryCount;
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            schema.IncludeInBuild = true;

            List<AddressableAssetEntry> toMove = new List<AddressableAssetEntry>();
            foreach (AddressableAssetGroup other in settings.groups)
            {
                if (other == null || other == group)
                {
                    continue;
                }

                foreach (AddressableAssetEntry entry in other.entries)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.address) && PartAddress.IsMatch(entry.address))
                    {
                        toMove.Add(entry);
                    }
                }
            }

            foreach (AddressableAssetEntry entry in toMove)
            {
                settings.CreateOrMoveEntry(entry.guid, group);
            }

            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(group);
            AssetDatabase.SaveAssets();

            Debug.Log($"{nameof(MoveWorkshopArtToRemote)}.{nameof(Move)} moved {toMove.Count} entries into '{GroupName}' " +
                      $"(build={schema.BuildPath.GetValue(settings)}, load={schema.LoadPath.GetValue(settings)}, " +
                      $"timeout={schema.Timeout}s, retries={schema.RetryCount}). Group now holds {group.entries.Count} entries.");
        }
    }
}
#endif
