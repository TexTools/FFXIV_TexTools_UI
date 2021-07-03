using FFXIV_TexTools.Resources;
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
        private string _descriptionModpackName;
        private string _descriptionModpackAuthor;
        private string _descriptionModpackVersion;
        private string _descriptionModpackUrl;
        private string _descriptionModpackContent;

        public BackupModpackViewModel()
        {
        }

        public string DescriptionModpackName 
        { 
            get => _descriptionModpackName; 
            set { _descriptionModpackName = value; OnPropertyChanged(nameof(DescriptionModpackName)); } 
        }

        public string DescriptionModpackAuthor
        { 
            get => _descriptionModpackAuthor;
            set { _descriptionModpackAuthor = value; OnPropertyChanged(nameof(DescriptionModpackAuthor)); }
        }

        public string DescriptionModpackVersion
        { 
            get => _descriptionModpackVersion;
            set { _descriptionModpackVersion = value; OnPropertyChanged(nameof(DescriptionModpackVersion)); }
        }

        public string DescriptionModpackUrl
        { 
            get => _descriptionModpackUrl;
            set { _descriptionModpackUrl = value; OnPropertyChanged(nameof(DescriptionModpackUrl)); }
        }

        public string DescriptionModpackContent
        { 
            get => _descriptionModpackContent;
            set { _descriptionModpackContent = value; OnPropertyChanged(nameof(DescriptionModpackContent)); }
        }

        public void UpdateDescription(ModPack selectedModpack, List<Mod> modsInModpack)
        {
            DescriptionModpackName = selectedModpack?.name ?? UIStrings.Standalone_Non_ModPack;
            DescriptionModpackAuthor = selectedModpack?.author ?? "N/A";
            DescriptionModpackVersion = selectedModpack?.version ?? "N/A";
            DescriptionModpackUrl = selectedModpack?.url ?? "";
            DescriptionModpackContent = string.Empty;

            Task.Run(() =>
            {
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

                DescriptionModpackContent = Application.Current.Dispatcher.Invoke(() => contentString);
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class BackupModpackItemEntry : INotifyPropertyChanged
    {
        private string _modpackName;
        private bool _isChecked;

        public BackupModpackItemEntry(string modPackName)
        {
            _modpackName = modPackName;
            _isChecked = true;
        }

        public string ModpackName
        {
            get => _modpackName;
            set { _modpackName = value; }
        }

        public bool IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; OnPropertyChanged(nameof(IsChecked)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
