using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xivModdingFramework.Models.Helpers;
using xivModdingFramework.Mods;

namespace FFXIV_TexTools.Models
{
    public class TTProject 
    {
        public string Name;

        public ModTransactionSettings TransactionSettings;

        public List<string> PreparationModpacks = new List<string>();

        /// <summary>
        /// Internal File Path => External File Path
        /// </summary>
        public Dictionary<string, string> Files = new Dictionary<string, string>();


        /// <summary>
        /// External File Path => Last Modified Time
        /// </summary>
        public Dictionary<string, DateTime> LastModifiedTimes = new Dictionary<string, DateTime>();


        public Dictionary<string, SmartImportOptions> ImportOptions = new Dictionary<string, SmartImportOptions>();

        [JsonIgnore]
        public string JsonPath;
    }
}
