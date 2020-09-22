using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.SqPack.DataContainers;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for ModpackRootConvertWindow.xaml
    /// </summary>
    public partial class ModpackRootConvertWindow : Window
    {
        Dictionary<XivDependencyRoot, (XivDependencyRoot Root, int Variant)> Roots;
        Dictionary<XivDependencyRoot, List<IItemModel>> Items;

        ModList _modlist;
        Dictionary<XivDataFile, IndexFile> _indexFiles;


        public ModpackRootConvertWindow(List<string> filePaths, Dictionary<XivDataFile, IndexFile> indexFiles, ModList modList)
        {
            InitializeComponent();

            _indexFiles = indexFiles;
            _modlist = modList;


            // Async init function
            Init(filePaths);
        }

        private async Task Init(List<string> filePaths)
        {

            var metaFiles = filePaths.Where(x => x.EndsWith(".meta"));

            foreach (var file in metaFiles)
            {
                var root = await XivCache.GetFirstRoot(file);
                if (root != null)
                {
                    Roots.Add(root, (root, -1));
                    var items = await root.GetAllItems();
                    Items.Add(root, items);

                    var df = IOUtil.GetDataFileFromPath(root.Info.GetRootFile());

                    var models = await root.GetModelFiles(_indexFiles[df], _modlist);
                }
            }

            // For each root within the modpack, we need to do the following (All optional):
            // 1. Select a new root to place it on.
            // 2. Choose whether or not to copy variants over 1:1
            //      - Or if we should use a single variant.
            // 3. If we use a single variants, we have to choose which variant we want to keep.
        }
    }
}
