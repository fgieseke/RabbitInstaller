using System;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Serialization;

namespace RabbitCli.Infrastructure
{
    public class Utils
    {
        public static T LoadJson<T>(string fileName, bool silent = false)
        {
            if (!File.Exists(fileName))
            {
                if (!silent)
                {
                    Console.WriteLine($"File '{fileName}' does not exist.");
                }
                return default(T);
            }

            using (var r = new StreamReader(fileName))
            {
                var json = r.ReadToEnd();
                var config = JsonConvert.DeserializeObject<T>(json);
                return config;
            }
        }

        public static void SaveJsonToDisk<T>(string fileName, T envConfig)
        {
            var subDir = Path.GetDirectoryName(fileName);
            if (!Directory.Exists(subDir) && subDir != null)
            {
                Directory.CreateDirectory(subDir);
            }
            else if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            using (var str = new StreamWriter(fileName))
            {
                var jsonFile = JsonConvert.SerializeObject(envConfig, Formatting.Indented, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                });
                str.Write(jsonFile);
            }

        }

        public static void SaveToDisk(string fileName, string fileContent)
        {
            if (File.Exists(fileName))
                File.Delete(fileName);
            using (var str = new StreamWriter(fileName))
            {
                str.Write(fileContent);
            }

        }

        public static string GetSimulationFolderName(string simEnv)
        {
            return Path.Combine("SetupSim", simEnv);
        }

        public static string GetSimulationFileName(string simEnv, string exchange)
        {
            return Path.Combine("SetupSim", simEnv, $"{exchange}.json");
        }

        public static string GetExportFile(string referenceFile)
        {
            return Path.Combine("SetupSim", "Export", $"{referenceFile}.json");
        }

        public static string GetEnvFileName(string setupEnv)
        {
            return Path.Combine("SetupEnv", $"env.{setupEnv}.json");
        }

        public static string GetSetupFileName(string setupEnv)
        {
            return Path.Combine("Setup", $"setup.{setupEnv}.json");
        }


        public static string ReadFromConsole(string text, string defaultVal = null, string[] validValues = null)
        {
            string val;
            bool isValid;
            do
            {

                Console.Write($"{text}");
                if (defaultVal == "")
                {
                    Console.Write("(optional)");
                }

                Console.Write(": ");
                if (defaultVal != null)
                {
                    System.Windows.Forms.SendKeys.SendWait(defaultVal);
                }
                val = Console.ReadLine();
                if (val?.ToLower() == "x")
                    throw new Exception("User cancelled.");

                if (string.IsNullOrEmpty(val))
                {
                    if (defaultVal != null)
                        return defaultVal;

                    Console.WriteLine("You have to enter a value.");
                    isValid = false;
                }
                else if (validValues != null && !validValues.Select(v => v.ToLower()).Contains(val.ToLower()))
                {
                    Console.WriteLine("You have to enter one of the values: " + string.Join(", ", validValues));
                    isValid = false;
                }
                else
                {
                    isValid = true;
                }

            } while (!isValid);

            return val;
        }

        public static string GetBatchFileName(string batchFileName)
        {
            return Path.Combine("SetupBatch", $"{batchFileName}.bat");
        }
    }
}
