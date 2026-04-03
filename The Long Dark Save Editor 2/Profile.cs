using System.IO;
using System.Text.RegularExpressions;
using System;
using System.Globalization;
using System.Linq;
using The_Long_Dark_Save_Editor_2.Game_data;
using The_Long_Dark_Save_Editor_2.Helpers;
using The_Long_Dark_Save_Editor_2.Serialization;

namespace The_Long_Dark_Save_Editor_2
{
    public class Profile
    {
        private const int MAX_BACKUPS = 10;

        public string path;

        private DynamicSerializable<ProfileState> dynamicState;
        public ProfileState State { get { return dynamicState.Obj; } }
        private string lastSerializedForSave;

        public Profile(string path)
        {
            this.path = path;

            var json = EncryptString.Decompress(File.ReadAllBytes(path));

            // m_StatsDictionary is invalid json (unquoted keys), so fix it
            json = NormalizeStatsDictionaryJson(json);

            dynamicState = new DynamicSerializable<ProfileState>(json);
            lastSerializedForSave = SerializeForSave();
        }

        public void Save()
        {
            string json = SerializeForSave();

            if (json == lastSerializedForSave)
                return;

            Backup();
            File.WriteAllBytes(path, EncryptString.Compress(json));
            lastSerializedForSave = json;
        }

        private string SerializeForSave()
        {
            string json = dynamicState.Serialize();

            // Game cannot read valid json for m_StatsDictionary so remove quotes from keys.
            return DenormalizeStatsDictionaryJson(json);
        }

        private void Backup()
        {
            var backupDirectory = Path.Combine(Path.GetDirectoryName(path), "backups");
            Directory.CreateDirectory(backupDirectory);

            var oldBackups = new DirectoryInfo(backupDirectory).GetFiles().OrderByDescending(x => x.LastWriteTime).Skip(MAX_BACKUPS);
            foreach (var file in oldBackups)
            {
                File.Delete(file.FullName);
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss", CultureInfo.InvariantCulture);
            var i = 1;
            var backupPath = Path.Combine(backupDirectory, timestamp + "-" + Path.GetFileName(path) + ".backup");
            while (File.Exists(backupPath))
            {
                backupPath = Path.Combine(backupDirectory, timestamp + "-" + Path.GetFileName(path) + "(" + i++ + ")" + ".backup");
            }
            File.Copy(path, backupPath);
        }

        private static string NormalizeStatsDictionaryJson(string json)
        {
            return Regex.Replace(json, @"(\\*\""m_StatsDictionary\\*\"":\{)((?:[-0-9\.]+:\\*\""[-+0-9eE\.]+\\*\""\,?)+)(\})", delegate (Match match)
            {
                string jsonSubStr = Regex.Replace(match.Groups[2].ToString(), @"([-0-9]+):(\\*\"")", delegate (Match matchSub)
                {
                    var escapeStr = matchSub.Groups[2].ToString();
                    return escapeStr + matchSub.Groups[1].ToString() + escapeStr + @":" + escapeStr;
                });
                return match.Groups[1].ToString() + jsonSubStr + match.Groups[3].ToString();
            });
        }

        private static string DenormalizeStatsDictionaryJson(string json)
        {
            return Regex.Replace(json, @"(\\*\""m_StatsDictionary\\*\"":\{)((?:\\*\""[-0-9\.]+\\*\"":\\*\""[-+0-9eE\.]+\\*\""\,?)+)(\})", delegate (Match match)
            {
                string jsonSubStr = Regex.Replace(match.Groups[2].ToString(), @"\\*\""([-0-9]+)\\*\"":", delegate (Match matchSub)
                {
                    return matchSub.Groups[1].ToString() + @":";
                });
                return match.Groups[1].ToString() + jsonSubStr + match.Groups[3].ToString();
            });
        }
    }
}
