
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using static xivModdingFramework.Materials.DataContainers.ShaderHelpers;
using System.IO;
using System.Linq;
using HelixToolkit.SharpDX.Core.Shaders;
using xivModdingFramework.Materials.DataContainers;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;

namespace FFXIV_TexTools.Views.Controls
{
    public partial class ShaderKeysEditor : Window
    {
        public class WrappedKey : INotifyPropertyChanged
        {

            public WrappedKey(ShaderKeysEditor editor, ShaderKey key)
            {
                _Editor = editor;
                Key = key;
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private ShaderKeysEditor _Editor;

            public ShaderKey Key;
            public uint KeyId
            {
                get
                {
                    return Key.KeyId;
                }
                set
                {
                    Key.KeyId = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KeyId)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KnownValues)));

                    var ninfo = Key.GetKeyInfo(_Editor.Material.ShaderPack);
                    if (ninfo != null)
                    {
                        Value = ninfo.Value.DefaultValue;
                    }
                    else
                    {
                        Value = 0;
                    }
                }
            }
            public uint Value
            {
                get
                {
                    return Key.Value;
                }
                set
                {
                    Key.Value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
            public string Name
            {
                get
                {
                    var ninfo = Key.GetKeyInfo(_Editor.Material.ShaderPack);
                    var keystring = KeyId.ToString("X8");
                    if (ninfo == null)
                    {
                        return keystring;
                    }
                    var info = ninfo.Value;
                    return info.UIName;
                }
            }
            public ObservableCollection<KeyValuePair<string, uint>> KnownValues {
                get
                {
                    var ninfo = Key.GetKeyInfo(_Editor.Material.ShaderPack);
                    var values = new ObservableCollection<KeyValuePair<string, uint>>();
                    if (ninfo == null)
                    {
                        if(!values.Any(x => x.Value == Value))
                        {
                            values.Add(new KeyValuePair<string, uint>(Value.ToString("X8"), Value));
                        }
                        return values;
                    }
                    var info = ninfo.Value;

                    foreach (var val in info.KnownValues)
                    {
                        values.Add(new KeyValuePair<string, uint>(val.ToString("X8"), val));
                    }

                    if (!values.Any(x => x.Value == Value))
                    {
                        values.Add(new KeyValuePair<string, uint>(Value.ToString("X8"), Value));
                    }
                    return values;
                }
            }
            public ObservableCollection<KeyValuePair<string, uint>> AvailableKeys
            {
                get
                {
                    var col = new ObservableCollection<KeyValuePair<string, uint>>();
                    foreach(var kv in ShaderKeys[_Editor.Material.ShaderPack])
                    {
                        var info = kv.Value;
                        var kvp = new KeyValuePair<string, uint>(info.UIName, info.Key);
                        col.Add(kvp);
                    }

                    if (!col.Any(x => x.Value == KeyId))
                    {
                        col.Add(new KeyValuePair<string, uint>(KeyId.ToString("X8"), KeyId));
                    }
                    col = new ObservableCollection<KeyValuePair<string, uint>>(col.OrderBy(x => x.Key));
                    return col;
                }
            }
        }

        private ObservableCollection<WrappedKey> Keys;

        private XivMtrl Material;
        private XivMtrl _OriginalMaterial;
        public ShaderKeysEditor(XivMtrl material)
        {
            DataContext = this;
            _OriginalMaterial = material;
            Material = (XivMtrl) material.Clone();

            if(!ShaderKeys.ContainsKey(Material.ShaderPack))
            {
                // Safety catch.
                ShaderKeys[Material.ShaderPack] = new Dictionary<uint, ShaderKeyInfo>();
            }

            InitializeComponent();
            UpdateList();
        }

        private void UpdateList()
        {
            Keys = new ObservableCollection<WrappedKey>();
            foreach (var key in Material.ShaderKeys)
            {
                Keys.Add(new WrappedKey(this, (ShaderKey)key));
            }
            KeyList.ItemsSource = Keys;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            // Bind new keys.
            _OriginalMaterial.ShaderKeys = Material.ShaderKeys;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        public static bool ShowKeysEditor(XivMtrl material, Window owner = null)
        {
            var wind = new ShaderKeysEditor(material);
            wind.Owner = owner != null ? owner : System.Windows.Application.Current.MainWindow;
            wind.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var result = wind.ShowDialog();
            if(result != true)
            {
                return false;
            }
            return true;
        }

        private void RemoveKey_Click(object sender, RoutedEventArgs e)
        {
            var key = ((WrappedKey)((Button)sender).DataContext).Key;
            Material.ShaderKeys.Remove(key);
            UpdateList();
        }

        private void AddKey_Click(object sender, RoutedEventArgs e)
        {
            var newKey = new ShaderKey();

            if(ShaderKeys[Material.ShaderPack].Count > 0)
            {
                var info = ShaderKeys[Material.ShaderPack].First().Value;
                newKey.KeyId = info.Key;
                newKey.Value = info.DefaultValue;
            }
            Material.ShaderKeys.Add(newKey);
            UpdateList();
        }
    }
}
