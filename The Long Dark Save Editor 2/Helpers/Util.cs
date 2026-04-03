using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using The_Long_Dark_Save_Editor_2.Game_data;

namespace The_Long_Dark_Save_Editor_2.Helpers
{

    public class SlotDataDisplayNameProxy
    {
        public string m_DisplayName { get; set; }
    }

    public static class Util
    {
        private static readonly object IsDebug;
        private static readonly Regex SaveFileRegex = new Regex(@"^(?:(?:ep[0-9])?(?:sandbox|challenge|story|relentless|autosave|checkpoint)[0-9]+|quicksave)$", RegexOptions.IgnoreCase);
        private static readonly string[] ProfilePatterns = new[] { "profile_survival.*", "user001.*" };

        public static T DeserializeObject<T>(string json) where T : class
        {

            if (json == null)
                return null;

            return JsonConvert.DeserializeObject<T>(json);
        }

        public static T DeserializeObjectOrDefault<T>(string json) where T : class, new()
        {
            if (json == null)
                return new T();
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static string SerializeObject(object o)
        {
            if (o == null)
                return null;
            return JsonConvert.SerializeObject(o);
        }

        public static List<string> GetSaveDirectories(string gameFolder)
        {
            var directories = new List<string>();
            if (string.IsNullOrEmpty(gameFolder))
                return directories;

            foreach (var directory in new[] { gameFolder, Path.Combine(gameFolder, "Survival") })
            {
                if (Directory.Exists(directory))
                    directories.Add(directory);
            }

            return directories;
        }

        public static ObservableCollection<EnumerationMember> GetSaveFiles(string folder)
        {
            return GetSaveFiles(new[] { folder });
        }

        public static ObservableCollection<EnumerationMember> GetSaveFiles(IEnumerable<string> folders)
        {
            var saves = folders
                .Where(Directory.Exists)
                .SelectMany(folder => Directory.GetFiles(folder))
                .Where(file => SaveFileRegex.IsMatch(Path.GetFileName(file)))
                .Select(file => new FileInfo(file))
                .OrderByDescending(file => file.LastWriteTime)
                .Select(file => file.FullName)
                .Distinct()
                .ToList();

            var result = new ObservableCollection<EnumerationMember>();
            foreach (string saveFile in saves)
            {
                try
                {
                    var member = CreateSaveEnumerationMember(saveFile, Path.GetFileName(saveFile));
                    result.Add(member);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                    continue;
                }
            }

            return result;
        }

        public static List<string> GetProfileFiles(IEnumerable<string> folders)
        {
            return folders
                .Where(Directory.Exists)
                .SelectMany(folder => ProfilePatterns.SelectMany(pattern => Directory.GetFiles(folder, pattern)))
                .Distinct()
                .Select(file => new FileInfo(file))
                .OrderByDescending(file => file.LastWriteTime)
                .Select(file => file.FullName)
                .ToList();
        }

        private static EnumerationMember CreateSaveEnumerationMember(string file, string name)
        {
            var member = new EnumerationMember();
            member.Value = file;

            var slotJson = EncryptString.Decompress(File.ReadAllBytes(file));
            var slotData = JsonConvert.DeserializeObject<SlotDataDisplayNameProxy>(slotJson);

            member.Description = slotData.m_DisplayName + " (" + name + ")";

            return member;
        }

        public static string GetLocalPath()
        {
            Guid localLowId = new Guid("A520A1A4-1780-4FF6-BD18-167343C5AF16");
            return GetKnownFolderPath(localLowId).Replace("LocalLow", "Local");
        }

        private static string GetKnownFolderPath(Guid knownFolderId)
        {
            IntPtr pszPath = IntPtr.Zero;
            try
            {
                int hr = SHGetKnownFolderPath(knownFolderId, 0, IntPtr.Zero, out pszPath);
                if (hr >= 0)
                    return Marshal.PtrToStringAuto(pszPath);
                throw Marshal.GetExceptionForHR(hr);
            }
            finally
            {
                if (pszPath != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pszPath);
            }
        }

        [DllImport("shell32.dll")]
        private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr pszPath);
    }
}
