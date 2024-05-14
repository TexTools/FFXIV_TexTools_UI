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

            ItemName.Content = "Final Modpack Review".L();
            FilesReviewLabel.Content = $"Review [{_vm.TotalFileCount._()}] Total File(s)".L();
            SharedInit();
            LoadVMFiles();
        }

        public StandardModpackFilesReview(StandardModpackItemEntry entry)
        {
            _entry = entry;
            InitializeComponent();
            ConfirmButton.IsEnabled = false;

            ItemName.Content = $"{_entry.Item.Name._()} - {StandardModpackCreator.GetNiceLevelName(_entry.Level)._()} Level".L();
            FilesReviewLabel.Content = "[Loading]".L();

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
            var tx = MainWindow.DefaultTransaction;

            if (Path.GetExtension(file) != ".meta")
            {
                if (!(await tx.FileExists(file)))
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
            else if (match.Success && (match.Groups[1].Value == "mtrl" || match.Groups[1].Value == "avfx"))
            {

                MaterialListBox.Items.Add(new StandardModpackFileSelect.FileEntry(file));
            }
            else if (match.Success && (match.Groups[1].Value == "tex" || match.Groups[1].Value == "atex"))
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

            MetaLabel.Content = $"{MetaListBox.Items.Count._()} Meta File(s)".L();
            ModelLabel.Content = $"{ModelListBox.Items.Count._()} Model File(s)".L();
            MaterialLabel.Content =$"{MaterialListBox.Items.Count._()} Material File(s)".L();
            TextureLabel.Content = $"{TextureListBox.Items.Count._()} Texture File(s)".L();

            if (FinalReviewMode)
            {
                FilesReviewLabel.Content = $"Review [{_vm.AllFiles.Count._()}] Total File(s)".L();
            }
            else
            {
                FilesReviewLabel.Content = $"Review [{_entry.AllFiles.Count._()}] Total File(s)".L();
            }

        }
        private async Task LoadFiles()
        {
            var parentFiles = _entry.MainFiles;
            var files = new SortedSet<string>();
            if (_entry.Level == XivDependencyLevel.Root)
            {
                var root = await XivCache.GetFirstRoot(_entry.MainFiles[0]);
                files = await root.GetAllFiles();
            }
            else
            {
                foreach (var file in parentFiles)
                {
                    var children = await XivCache.GetChildrenRecursive(file);
                    foreach (var child in children)
                    {
                        files.Add(child);
                    }
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
