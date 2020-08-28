using FFXIV_TexTools.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using xivModdingFramework.Cache;
using xivModdingFramework.SqPack.FileTypes;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for CompleteModpackFilesReview.xaml
    /// </summary>
    public partial class StandardModpackFilesReview : Page
    {
        private StandardModpackViewModel _vm;
        private StandardModpackItemEntry _entry;

        private bool FinalReviewMode
        {
            get
            {
                return _vm != null;
            }
        }

        public event EventHandler<StandardModpackItemEntry> ReviewAccepted;

        public event EventHandler<StandardModpackViewModel> FinalReviewAccepted;
        public StandardModpackFilesReview(StandardModpackViewModel vm)
        {
            _vm = vm;
            InitializeComponent();
            ConfirmButton.IsEnabled = false;

            ItemName.Content = "Final Modpack Review";
            FilesReviewLabel.Content = "Review [" + _vm.TotalFileCount + "] Total File(s)";
            SharedInit();
            LoadVMFiles();
        }

        public StandardModpackFilesReview(StandardModpackItemEntry entry)
        {
            _entry = entry;
            InitializeComponent();
            ConfirmButton.IsEnabled = false;

            ItemName.Content = _entry.Item.Name + " - " + StandardModpackCreator.GetNiceLevelName(_entry.Level) + " Level";
            FilesReviewLabel.Content = "[Loading]";

            SharedInit();
            LoadFiles();
        }

        private void SharedInit()
        {
            ConfirmButton.Click += ConfirmButton_Click;
            BackButton.Click += BackButton_Click;

            MetaListBox.DisplayMemberPath = "DisplayName";
            MetaListBox.SelectedValuePath = "File";

            ModelListBox.DisplayMemberPath = "DisplayName";
            ModelListBox.SelectedValuePath = "File";

            MaterialListBox.DisplayMemberPath = "DisplayName";
            MaterialListBox.SelectedValuePath = "File";

            TextureListBox.DisplayMemberPath = "DisplayName";
            TextureListBox.SelectedValuePath = "File";
        }

        private static readonly Regex _suffixRegex = new Regex("\\.([a-z]+)$");

        private async Task LoadVMFiles()
        {
            foreach (var file in _vm.AllFiles)
            {
                await AddFile(file);
            }
            ConfirmButton.IsEnabled = true;
        }
        
        private async Task<bool> AddFile(string file)
        {
            var _index = new Index(XivCache.GameInfo.GameDirectory);

            if (Path.GetExtension(file) != ".meta")
            {
                if (!(await _index.FileExists(file)))
                {
                    // File doesn't actually exist, can't be added.
                    return false;
                }
            }

            var match = _suffixRegex.Match(file);
            if (match.Success && match.Groups[1].Value == "mdl")
            {
                ModelListBox.Items.Add(new StandardModpackFileSelect.FileEntry(file));
            }
            else if (match.Success && match.Groups[1].Value == "mtrl")
            {

                MaterialListBox.Items.Add(new StandardModpackFileSelect.FileEntry(file));
            }
            else if (match.Success && match.Groups[1].Value == "tex")
            {
                TextureListBox.Items.Add(new StandardModpackFileSelect.FileEntry(file));
            }
            else
            {
                MetaListBox.Items.Add(new StandardModpackFileSelect.FileEntry(file));
            }
            return true;
        }

        private void UpdateCounts()
        {

            MetaLabel.Content = MetaListBox.Items.Count + " Meta File(s)";
            ModelLabel.Content = ModelListBox.Items.Count + " Model File(s)";
            MaterialLabel.Content = MaterialListBox.Items.Count + " Material File(s)";
            TextureLabel.Content = TextureListBox.Items.Count + " Texture File(s)";

            if (FinalReviewMode)
            {
                FilesReviewLabel.Content = "Review [" + _vm.AllFiles.Count + "] Total File(s)";
            }
            else
            {
                FilesReviewLabel.Content = "Review [" + _entry.AllFiles.Count + "] Total File(s)";
            }

        }
        private async Task LoadFiles()
        {
            var parentFiles = _entry.MainFiles;
            var files = new SortedSet<string>();
            foreach(var file in parentFiles)
            {
                var children = await GetChildrenRecursive(file);
                foreach(var child in children)
                {
                    files.Add(child);
                }
            }


            _entry.AllFiles.Clear();
            foreach (var file in files)
            {
                var exists = await AddFile(file);
                if(exists)
                {
                    _entry.AllFiles.Add(file);
                }
            }

            UpdateCounts();

            ConfirmButton.IsEnabled = true;
        }

        private async Task<HashSet<string>> GetChildrenRecursive(string file)
        {
            var files = new HashSet<string>();
            files.Add(file);

            var baseChildren = await XivCache.GetChildFiles(file);
            if(baseChildren == null || baseChildren.Count == 0)
            {
                // No children, just us.
                return files;
            } else
            {
                // We have child files.
                foreach(var child in baseChildren)
                {
                    // Recursively get their children.
                    var children = await GetChildrenRecursive(child);
                    foreach(var subchild in children)
                    {
                        // Add the results to the list.
                        files.Add(subchild);
                    }
                }
            }
            return files;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (FinalReviewMode)
            {
                if (FinalReviewAccepted != null)
                {
                    FinalReviewAccepted.Invoke(this, null);
                }
            }
            else
            {
                if (ReviewAccepted != null)
                {
                    ReviewAccepted.Invoke(this, null);
                }
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (FinalReviewMode)
            {
                if (FinalReviewAccepted != null)
                {
                    FinalReviewAccepted.Invoke(this, _vm);
                }
            }
            else
            {
                if (ReviewAccepted != null)
                {
                    ReviewAccepted.Invoke(this, _entry);
                }
            }
        }
    }
}
