using FFXIV_TexTools.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xivModdingFramework.Cache;
using xivModdingFramework.Items.Interfaces;

namespace FFXIV_TexTools.ViewModels
{
    /// <summary>
    /// Class representing all of the data necessary to drive the Create Standard Modpack view chain.
    /// </summary>
    public class StandardModpackViewModel
    {
        public StandardModpackViewModel()
        {
        }

        public string Name = "Standard Modpack";
        public string Author = "TexTools User";
        public Version Version = new Version("1.0.0");
        public string Description = "";
        public string Url = "";
        public bool SaveAdvanced = false;

        public string DestinationFilePath;
        public ObservableCollection<StandardModpackItemEntry> Entries = new ObservableCollection<StandardModpackItemEntry>();
        public int TotalFileCount
        {
            get
            {
                int count = 0;
                foreach (var e in Entries)
                {
                    count += e.AllFiles.Count;
                }
                return count;
            }
        }

        public SortedSet<string> AllFiles
        {
            get
            {
                var files = new SortedSet<string>();
                foreach (var entry in Entries)
                {
                    foreach (var file in entry.AllFiles)
                    {
                        files.Add(file);
                    }
                }
                return files;
            }
        }
    }


    /// <summary>
    /// Entry representing a single pass through of all the pages by a user to select the item, level, and file subset.
    /// </summary>
    public class StandardModpackItemEntry
    {
        public readonly IItem Item;
        public readonly XivDependencyLevel Level;
        public readonly ObservableCollection<string> MainFiles = new ObservableCollection<string>();
        public readonly ObservableCollection<string> AllFiles = new ObservableCollection<string>();

        public StandardModpackItemEntry(IItem item, XivDependencyLevel level, ObservableCollection<string> mainFiles = null, ObservableCollection<string> allFiles = null)
        {
            Item = item;
            Level = level;
            if(mainFiles == null)
            {
                MainFiles = new ObservableCollection<string>();
            } else
            {
                MainFiles = mainFiles;
            }

            if (allFiles == null)
            {
                AllFiles = new ObservableCollection<string>();
            }
            else
            {
                AllFiles = allFiles;
            }
        }
    }
}
