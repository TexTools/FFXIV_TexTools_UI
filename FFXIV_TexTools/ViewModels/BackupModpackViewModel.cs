using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using xivModdingFramework.Mods.DataContainers;

namespace FFXIV_TexTools.ViewModels
{
    public class BackupModpackViewModel: INotifyPropertyChanged
    {
        private readonly ModList _modList;
        private string _modpackName;
        private string _modpackAuthor;
        private string _modpackVersion;
        private string _modpackUrl;
        private string _modpackContent;

        public BackupModpackViewModel(ModList modList)
        {
            _modList = modList;
        }

        public string ModpackName 
        { 
            get => _modpackName; 
            set { _modpackName = value; OnPropertyChanged(nameof(ModpackName)); } 
        }

        public string ModpackAuthor 
        { 
            get => _modpackAuthor;
            set { _modpackAuthor = value; OnPropertyChanged(nameof(ModpackAuthor)); }
        }

        public string ModpackVersion 
        { 
            get => _modpackVersion;
            set { _modpackVersion = value; OnPropertyChanged(nameof(ModpackVersion)); }
        }

        public string ModpackUrl 
        { 
            get => _modpackUrl;
            set { _modpackUrl = value; OnPropertyChanged(nameof(ModpackUrl)); }
        }

        public string ModpackContent 
        { 
            get => _modpackContent;
            set { _modpackContent = value; OnPropertyChanged(nameof(ModpackContent)); }
        }

        public void UpdateDescription(ModPack selectedModpack)
        {
            ModpackName = selectedModpack.name;
            ModpackAuthor = selectedModpack.author;
            ModpackVersion = selectedModpack.version;
            ModpackUrl = selectedModpack.url;
            ModpackContent = string.Empty;

            Task.Run(() =>
            {
                var modsInModpack = (from mods in _modList.Mods
                                     where (mods.modPack != null && mods.modPack.name == selectedModpack.name)
                                     select mods).ToList();

                var modNameDict = new Dictionary<string, int>();

                foreach (var mod in modsInModpack)
                {
                    if (mod.IsInternal()) continue;

                    if (!modNameDict.ContainsKey(mod.name))
                    {
                        modNameDict.Add(mod.name, 1);
                    }
                    else
                    {
                        modNameDict[mod.name] += 1;
                    }
                }

                var contentString = string.Empty;

                foreach (var mod in modNameDict)
                {
                    contentString += $"[{ mod.Value}] {mod.Key}\n";
                }

                ModpackContent = Application.Current.Dispatcher.Invoke(() => contentString);
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class BackupModpackItemEntry
    {
        public BackupModpackItemEntry(ModPack modPack)
        {
            _modpackName = modPack.name;
            _modpackAuthor = modPack.author;
            _modpackUrl = modPack.url;
            _modpackVersion = modPack.version;
            _modpackContent = string.Empty;
        }

        private string _modpackName;
        private string _modpackAuthor;
        private string _modpackVersion;
        private string _modpackUrl;
        private string _modpackContent;
        private bool _isChecked;

        public string ModpackName
        {
            get => _modpackName;
            set { _modpackName = value; }
        }

        public string ModpackAuthor
        {
            get => _modpackAuthor;
            set { _modpackAuthor = value; }
        }

        public string ModpackVersion
        {
            get => _modpackVersion;
            set { _modpackVersion = value; }
        }

        public string ModpackUrl
        {
            get => _modpackUrl;
            set { _modpackUrl = value; }
        }

        public string ModpackContent
        {
            get => _modpackContent;
            set { _modpackContent = value; }
        }

        public bool IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; }
        }
    }
}
