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
        public StandardModpackViewModel(StandardModpackCreator creator)
        {
            Creator = creator;
        }

        public readonly StandardModpackCreator Creator;
        public string Name;
        public string DestinationFilePath;
        public ObservableCollection<StandardModpackItemEntry> Entries;
    }


    /// <summary>
    /// Entry representing a single pass through of all the pages by a user to select the item, level, and file subset.
    /// </summary>
    public class StandardModpackItemEntry
    {
        public readonly IItem Item;
        public readonly XivDependencyLevel Level;
        public readonly ObservableCollection<string> Files = new ObservableCollection<string>();

        public StandardModpackItemEntry(IItem item, XivDependencyLevel level, ObservableCollection<string> files = null)
        {
            Item = item;
            Level = level;
            if(files == null)
            {
                Files = new ObservableCollection<string>();
            } else
            {
                Files = files;
            }

        }
    }
}
