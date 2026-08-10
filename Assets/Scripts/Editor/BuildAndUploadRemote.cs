#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Junkinnering.Editor
{
    /// <summary>
    /// One-click Task C helper: builds the Addressables content, then uploads the remote
    /// build output (image bundles + the self-hosted catalog and its .hash) to the S3 prefix
    /// the RemoteLoadPath points at. Editor-only, non-shipping. The manual two-step sequence
    /// (Addressables ▸ Groups ▸ Build, then `aws s3 sync`) is the documented deliverable — this
    /// just chains it. Requires the AWS CLI on PATH with credentials that can write to
    /// s3://epochreels-ota/junkinnering/. Run only after the delivery host is probe-confirmed
    /// (see REMOTE_DELIVERY.md §"Prove the host first").
    /// </summary>
    public static class BuildAndUploadRemote
    {
        // Mirrors ServerData/[BuildTarget] onto junkinnering/[BuildTarget] under the shared,
        // private epochreels-ota bucket (served over HTTPS via CloudFront + OAC). Android-only,
        // matching the project's build target.
        private const string ServerDataDir = "ServerData/Android";
        private const string S3Target = "s3://epochreels-ota/junkinnering/Android/";
        private const string Region = "us-east-1";

        // The CLI's default profile does not carry credentials that can write to this bucket, so the
        // sync must name the profile explicitly — without it the upload fails with InvalidAccessKeyId.
        private const string AwsProfile = "epochreels";

        [MenuItem("Tools/Addressables/Build & Upload Remote")]
        public static void BuildAndUpload()
        {
            Debug.Log($"{nameof(BuildAndUploadRemote)}.{nameof(BuildAndUpload)} building Addressables content...");
            AddressableAssetSettings.BuildPlayerContent();

            // BuildPlayerContent logs its own errors to the console. The presence of the remote
            // output directory is the signal that the remote group actually produced bundles +
            // catalog to upload; if it's missing, the build failed or the Remote Images group
            // isn't pointed at RemoteBuildPath.
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string source = Path.Combine(projectRoot, ServerDataDir);
            if (!Directory.Exists(source))
            {
                Debug.LogError($"{nameof(BuildAndUploadRemote)}.{nameof(BuildAndUpload)} remote build output not found at '{source}' — did the build fail, or is the Remote Images group's BuildPath set to RemoteBuildPath?");
                return;
            }

            Debug.Log($"{nameof(BuildAndUploadRemote)}.{nameof(BuildAndUpload)} uploading '{source}' -> {S3Target}");
            RunAwsSync(source, S3Target);
        }

        private static void RunAwsSync(string source, string target)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "aws",
                Arguments = $"s3 sync \"{source}\" {target} --region {Region} --profile {AwsProfile}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            try
            {
                using (Process process = Process.Start(startInfo))
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        Debug.Log($"{nameof(BuildAndUploadRemote)}.{nameof(RunAwsSync)} upload complete:\n{stdout}");
                    }
                    else
                    {
                        Debug.LogError($"{nameof(BuildAndUploadRemote)}.{nameof(RunAwsSync)} aws s3 sync failed (exit {process.ExitCode}):\n{stderr}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{nameof(BuildAndUploadRemote)}.{nameof(RunAwsSync)} could not start the AWS CLI — is it installed and on PATH? {ex.Message}");
            }
        }
    }
}
#endif
