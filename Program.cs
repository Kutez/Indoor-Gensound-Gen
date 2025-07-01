using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Globalization;

namespace AMP.src
{
    class Program
    {
        public enum OperationTypes: int {
            All = 0,
            Convert = 1,
            Add_Comment = 2,
        }

        public static List<string> ConfigKeys = new List<string> {
            "GamedataDir", "FfmpegBinDir", "InputDir", "GameSndType", "BaseSndVol",
            "MinDist", "MaxDist", "MaxAIDist", "OutputDir", "RemoveWav", "Operation"
        };

        public static Dictionary<string, uint> sndTypeTbl = new Dictionary<string, uint>()
        {
            {"world_ambient", 134217856},
            {"object_exploding", 134217984},
            {"object_colliding", 134218240},
            {"object_breaking", 134218752},
            {"anomaly_idle", 268437504},
            {"npc_eating", 536875008},
            {"npc_attacking", 536879104},
            {"npc_talking", 536887296},
            {"npc_step", 536903680},
            {"npc_injuring", 536936448},
            {"npc_dying", 537001984},
            {"item_using", 1077936128},
            {"item_taking", 1082130432},
            {"item_hiding", 1090519040},
            {"item_dropping", 1107296256},
            {"item_picking_up", 1140850688},
            {"weapon_recharging", 2147745792},
            {"weapon_bullet_hit", 2148007936},
            {"weapon_empty_clicking", 2148532224},
            {"weapon_shooting", 2149580800},
            {"undefined", 0}
        };

        public static Dictionary<string, Dictionary<string, float>> OggHeaderPairs = new Dictionary<string, Dictionary<string, float>>
        {
            {
                "BaseSndVol",
                new Dictionary<string, float>
                {
                    {"min", 0.0f},
                    {"max", 2.0f}
                }
            },
            {
                "MinDist",
                new Dictionary<string, float>
                {
                    {"min", 0.0f},
                    {"max", 1000.0f}
                }
            },
            {
                "MaxDist",
                new Dictionary<string, float>
                {
                    {"min", 0.0f},
                    {"max", 1000.0f}
                }
            },
            {
                "MaxAIDist",
                new Dictionary<string, float>
                {
                    {"min", 0.0f},
                    {"max", 1000.0f}
                }
            }
            
        };
    
        [DllImport(@"add_ogg_comment.dll")]
        public static extern int amp_AddOggComment([MarshalAs(UnmanagedType.LPStr)]string path, float mindist, float maxdist, float basevol, uint sndtype, float maxaidist);
        
        // Config
        public static Dictionary<string, string> ReadConfig()
        {
            var fileDirectory = Path.GetDirectoryName(AppContext.BaseDirectory);
            var filePath = $"{fileDirectory}\\config.ini";
            Console.Write($"Reading Config from: {filePath}\n");
            if (File.Exists(filePath) == false)
                throw new Exception($"The Config.ini file is missing at {filePath}.");
            
            var fileContent = File.ReadLines(filePath);
            List<string> lines = new List<string>(fileContent);
            var dict = new Dictionary<string, string>();            
            List<string> PathKeys = new List<string> {"GamedataDir", "FfmpegBinDir", "InputDir", "OutputDir"};

            var validationMessage = new StringBuilder();

            foreach (string line in lines)
            {
                var split = line.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

                if (split.Length != 2)
                    continue;
                
                if (dict.ContainsKey(split[0].Trim()) == false)
                {
                    var key = split[0].Trim();
                    var val = split[1].Trim();

                    if (val == null)
                    {
                        if (key == "RemoveWav")
                        {
                            dict.Add(key, "false");
                        }
                        else if (key == "OutputDir" && dict.ContainsKey("GamedataDir"))
                        {
                            dict.Add("OutputDir",  $"{dict["GamedataDir"]}\\sounds");
                            Console.Write($"Setting 'OutputDir' to {dict["OutputDir"]}\n");
                            continue;
                        }

                        validationMessage.AppendLine($"Value for: '{key}' is missing\n");
                        continue;
                    }

                    if (key == "GameSndType" && !sndTypeTbl.ContainsKey(val))
                    {
                        string validSndTypes = "\n{";
                        int i = 1;
                        foreach (var kvp in sndTypeTbl)
                        {
                            validSndTypes += $"\t{i}. {kvp.Key}\n";
                            i += 1;
                        }
                        validSndTypes += "}";
                        validationMessage.AppendLine($"Invalid Game Sound Type: '{val}', Please Enter one of the following: {validSndTypes}\n");
                    }

                    if (!ConfigKeys.Contains(key))
                    {
                        validationMessage.AppendLine($"Invalid Config Key: '{key}', ignoring.\n");
                    }

                    if (PathKeys.Contains(key))
                    {
                        if (!Directory.Exists(val))
                        {
                            validationMessage.AppendLine($"Directory: {val} for Key: {key} is invalid\n");
                        }
                    }

                    if (OggHeaderPairs.ContainsKey(key))
                    {
                        float min_val = OggHeaderPairs[key]["min"];
                        float max_val = OggHeaderPairs[key]["max"];
                        float actual_val = stringToFloat(val);

                        if (actual_val < min_val || actual_val > max_val)
                        {
                            validationMessage.AppendLine($"Ogg Header Pair: {key}={actual_val} is outside of range {min_val}-{max_val}\n");
                        }
                    }

                    dict.Add(key, val);
                    Console.Write($"Read {key}={val}\n");
                }
            }

            if (!dict.ContainsKey("FfmpegBinDir") && stringToInt(dict["Operation"]) != 2)
            {
                validationMessage.AppendLine("Audio File Conversion Requires Ffmpeg. https://ffmpeg.org/download.html\n");
            }

            if (validationMessage.Length > 0)
            {
                validationMessage.AppendLine("Please refer to the README.md document to see valid fields for config.\n");
                throw new Exception(validationMessage.ToString());
            }

            return dict;
        }

        static int stringToInt(string str)
        {
            return Int32.Parse(str);
        }

        // Path Validation
        public static void ValidatePaths(Dictionary<string, string> config)
        {
            Console.Write("\nValidating Paths \n");
            foreach (var kvp in config)
            {                
                
                if (kvp.Key != "OutputDir" && Regex.IsMatch(kvp.Value, @"\w+:\\") && Directory.Exists(kvp.Value) == false)
                    throw new Exception($"The {kvp.Key} {kvp.Value} does not exist.\n");
            }
        }

        // Sound File Getter
        public static string[] GetSoundFiles(Dictionary<string, string> config)
        {
            var soundDir = $"{config["InputDir"]}";
            Console.Write($"Getting sound files from {soundDir} \n");
            var soundFiles = new List<string>();
            string[] suffixes = new string[] {"*.flac", "*.aac", "*.mp3", "*.wav", "*.ogg", "*.opus", "*.m4a"};
            foreach (var suffix in suffixes)
            {
                List<string> found_files =  new List<string>(Directory.GetFiles(soundDir, suffix, SearchOption.AllDirectories));
                soundFiles.AddRange(found_files);
            }

            return soundFiles.ToArray();
        }

        // Sound File Processes
        public static async Task ProcessSoundFiles(Dictionary<string, string> config, string[] soundFiles)
        {   
            var tasks = soundFiles.Select(path => ProcessSoundFile(config, path));
            await Task.WhenAll(tasks);
        }

        static async Task ProcessSoundFile(Dictionary<string, string> config, string path)
        {
            string ffmpegExe = GetFfmpegExe(config);
            string outpath_dir = config["OutputDir"];
            string input_dir = config["InputDir"];
            string ext = Path.GetExtension(path);
            string path_to_convert = path;

            if (path.StartsWith(input_dir, StringComparison.CurrentCultureIgnoreCase))
            {
                path = $"{outpath_dir}\\{path.Substring(input_dir.Length).TrimStart(Path.DirectorySeparatorChar)}";
            }
            
            string outpath_wav = $"{Path.GetDirectoryName(path)}\\{Path.GetFileNameWithoutExtension(path)}.wav";
            string outpath_ogg = $"{Path.GetDirectoryName(path)}\\{Path.GetFileNameWithoutExtension(path)}.ogg";

            int operation_type = stringToInt(config["Operation"]);
            switch (operation_type)
            {
                case (int) OperationTypes.All:
                    Console.Write("Converting and Adding Comment(s)\n");
                    outpath_ogg = await OperationConvert(ext, path_to_convert, path, outpath_wav, outpath_ogg, ffmpegExe);
                    OperationAddComment(config, outpath_ogg, outpath_wav);
                    return;

                case (int) OperationTypes.Convert:
                    Console.Write("Just Converting\n");
                    await OperationConvert(ext, path_to_convert, path, outpath_wav, outpath_ogg, ffmpegExe);
                    return;

                case (int) OperationTypes.Add_Comment:
                    Console.Write("Just Adding Comments\n");
                    OperationAddComment(config, path_to_convert, null);
                    return;
            }
        }

        static async Task<string> OperationConvert(string ext, string path_to_convert, string path, string outpath_wav, string outpath_ogg, string ffmpegExe)
        {
            if (ext == ".ogg") { return outpath_ogg; }

            bool is_wav = (ext == ".wav") ? true : false;

            if (is_wav){
                outpath_wav = path_to_convert;
                outpath_ogg = $"{Path.GetDirectoryName(path)}\\{Path.GetFileNameWithoutExtension(path_to_convert)}.ogg";
            }

            if (File.Exists(outpath_ogg))
            {
                Console.Write($"Ogg File Already Exists at: {outpath_ogg}\n");
                return outpath_ogg;
            }

            if (!is_wav) {
                string file_format = ext.Substring(1, ext.Length);
                await ConvertSoundFile(ffmpegExe, "-y -f {file_format}", "-acodec pcm_s16le -vn -f wav", path_to_convert, outpath_wav);
            }

            await ConvertSoundFile(ffmpegExe, "-y -f wav", "-acodec libvorbis -y -vn -ac 1 -ar 44100 -f ogg", outpath_wav, outpath_ogg);
            return outpath_ogg;
        }

        static string GetFfmpegExe(Dictionary<string, string> config)
        {
            var exeLocation = $"{config["FfmpegBinDir"]}\\ffmpeg.exe";
            //Console.Write($"Searching for ffmpeg exe at {exeLocation} \n");
            var exeLocationFileInfo = new FileInfo(exeLocation);
            if (exeLocationFileInfo.Exists)
            {
                //Console.Write($"Found ffmpeg exe \n");
                return exeLocation;
            }

            throw new Exception($"Could not find {exeLocation}. \n Please Install Ffmpeg or correct the 'FfmpegBinDir' value to ffmpeg's bin folder\n");
        }

        static async Task ConvertSoundFile(string ffmpegExe, string i_args, string o_args, string path, string outpath)
        {
            Console.Write($"Converting '{path}' to '{outpath}'\n");

            var directory = Path.GetDirectoryName(outpath);
            if (directory != null && !Directory.Exists(directory))
            {
                Console.Write($"Output Directory: {directory} doesn't exist\n");
                Directory.CreateDirectory(directory);
                Console.Write($"Created Directory: {directory}\n");
            }

            Process FfmpegProcess = new Process();
            FfmpegProcess.StartInfo.FileName = ffmpegExe;
            FfmpegProcess.StartInfo.Arguments = $"-hide_banner -log_level error {i_args} -i \"{path}\" {o_args} \"{outpath}\"";
            FfmpegProcess.StartInfo.RedirectStandardError = true;
            FfmpegProcess.StartInfo.RedirectStandardOutput = true;
            FfmpegProcess.StartInfo.UseShellExecute = false;
            FfmpegProcess.StartInfo.CreateNoWindow = false;
            FfmpegProcess.EnableRaisingEvents = true;
            FfmpegProcess.Start();

            await Task.Run(() => {
                Console.WriteLine(FfmpegProcess.StandardError.ReadToEnd());
            });

            await FfmpegProcess.WaitForExitAsync();
            Console.Write($"Finished Converted '{path}' to '{outpath}' \n");
            
        }

        static void OperationAddComment(Dictionary<string, string> config, string ogg_path, string? outpath_wav)
        {
            string ext = Path.GetExtension(ogg_path);
            if (ext != ".ogg") {return ;}

            AddOggComment(config, ogg_path);
            if (outpath_wav != null && bool.Parse(config["RemoveWav"]) && File.Exists(outpath_wav))
            {
                RemoveFile(outpath_wav);
            }
        }

        static void AddOggComment(Dictionary<string, string> config, string path)
        {
            if (!File.Exists(path))
            {
                Console.Write($"Cannot add ogg comment to {path} \n");
                return;
            }
            
            float basevol = stringToFloat(config["BaseSndVol"]);
            float mindist = stringToFloat(config["MinDist"]);
            float maxdist = stringToFloat(config["MaxDist"]);
            float maxaidist = stringToFloat(config["MaxAIDist"]);
            
            uint sndtype = gameSndTypeToInt(config["GameSndType"]);
            Console.Write($"Adding Ogg Comment: MinDist:{mindist}, MaxDist:{maxdist}, BaseVol:{basevol}, SndType:{sndtype}, MaxAIDist:{maxaidist} \n");
            amp_AddOggComment(path, mindist, maxdist, basevol, sndtype, maxaidist);
            Console.Write($"\nOgg Comment Added to {path}\n");
        }

        static float stringToFloat(string str)
        {
            return float.Parse(str, CultureInfo.InvariantCulture.NumberFormat);
        }

        static uint gameSndTypeToInt(string sndType) 
        {
            return sndTypeTbl[sndType];
        }

        static void RemoveFile(string srcFile)
        {
            if (!File.Exists(srcFile))
            {
                throw new Exception($"Cannot delete {srcFile} as it doesn't exist.\n");
            }
            File.Delete(srcFile);
        }

        // Extra
        static void MoveFile(Dictionary<string, string> config, string srcFile)
        {
            string outDir = config["OutputDir"];
            string destFile = $"{outDir}\\{Path.GetFileName(srcFile)}";
            Console.Write($"Moving {srcFile} to {destFile}\n");
            var moved = true;
            while (moved == false)
            {
                try
                {
                    if (!Directory.Exists(outDir))
                    {
                        Directory.CreateDirectory(outDir);
                    }
                    if (!File.Exists(destFile))
                    {
                        File.Move(srcFile, destFile);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"The process failed: {0}\n", e.ToString());
                }
            }
        }

         

        

        
        

        public static async Task Main(string[] args)
        {
            try
            {
                var config = ReadConfig();
                ValidatePaths(config);
                string [] soundFiles = GetSoundFiles(config);
                await ProcessSoundFiles(config, soundFiles);
                Console.Write("Finished Processing Sound Files");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.ReadKey();
            }
        }
    }
}