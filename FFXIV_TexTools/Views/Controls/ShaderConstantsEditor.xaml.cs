
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
using static FFXIV_TexTools.Views.Controls.ShaderKeysEditor;
using System.Windows.Controls;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for RawFloatValueDisplay.xaml
    /// </summary>
    public partial class ShaderConstantsEditor : Window
    {
        public class WrappedConstant : INotifyPropertyChanged
        {

            public WrappedConstant(ShaderConstantsEditor editor, ShaderConstant constant)
            {
                _Editor = editor;
                Constant = constant;
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private ShaderConstantsEditor _Editor;

            public ShaderConstant Constant;
            public uint ConstantId
            {
                get
                {
                    return Constant.ConstantId;
                }
                set
                {
                    Constant.ConstantId = value;

                    // Everything changes when ID changes.
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConstantId)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Values)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Val0)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Val1)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Val2)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Val3)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Val0Enabled)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Val1Enabled)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Val2Enabled)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Val3Enabled)));

                    var ninfo = Constant.GetConstantInfo(_Editor.Material.Shader);
                    if (ninfo != null)
                    {
                        Values = ninfo.Value.DefaultValues;
                    }
                    else
                    {
                        Values = new List<float>() { 0.0f };
                    }
                }
            }
            public List<float> Values
            {
                get
                {
                    return Constant.Values;
                }
                set
                {
                    Constant.Values = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Values)));
                }
            }
            public string Name
            {
                get
                {
                    var ninfo = Constant.GetConstantInfo(_Editor.Material.Shader);
                    var keystring = ConstantId.ToString("X8");
                    if (ninfo == null)
                    {
                        return keystring;
                    }
                    var info = ninfo.Value;
                    return info.UIName;
                }
            }
            public ObservableCollection<KeyValuePair<string, uint>> AvailableIds
            {
                get
                {
                    var col = new ObservableCollection<KeyValuePair<string, uint>>();
                    foreach (var kv in ShaderConstants[_Editor.Material.Shader])
                    {
                        var info = kv.Value;
                        var kvp = new KeyValuePair<string, uint>(info.UIName, info.Id);
                        col.Add(kvp);
                    }

                    if (!col.Any(x => x.Value == ConstantId))
                    {
                        col.Add(new KeyValuePair<string, uint>(ConstantId.ToString("X8"), ConstantId));
                    }

                    col = new ObservableCollection<KeyValuePair<string, uint>>(col.OrderBy(x => x.Key));
                    return col;
                }
            }


            public float Val0
            {
                get
                {
                    const int id = 0;
                    if (Values.Count <= id)
                    {
                        return 0.0f;
                    }
                    return Values[id];

                } set
                {
                    const int id = 0;
                    if (Values.Count <= id)
                    {
                        return;
                    }
                    Values[id] = value;
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Val0)));
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Values)));
                }
            }
            public float Val1
            {
                get
                {
                    const int id = 1;
                    if (Values.Count <= id)
                    {
                        return 0.0f;
                    }
                    return Values[id];

                }
                set
                {
                    const int id = 1;
                    if (Values.Count <= id)
                    {
                        return;
                    }
                    Values[id] = value;
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Val1)));
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Values)));
                }
            }
            public float Val2
            {
                get
                {
                    const int id = 2;
                    if (Values.Count <= id)
                    {
                        return 0.0f;
                    }
                    return Values[id];

                }
                set
                {
                    const int id = 2;
                    if (Values.Count <= id)
                    {
                        return;
                    }
                    Values[id] = value;
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Val2)));
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Values)));
                }
            }
            public float Val3
            {
                get
                {
                    const int id = 3;
                    if (Values.Count <= id)
                    {
                        return 0.0f;
                    }
                    return Values[id];

                }
                set
                {
                    const int id = 3;
                    if (Values.Count <= id)
                    {
                        return;
                    }
                    Values[id] = value;
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Val3)));
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Values)));
                }
            }

            public bool Val0Enabled
            {
                get
                {
                    return Values.Count > 0;
                }
            }
            public bool Val1Enabled
            {
                get
                {
                    return Values.Count > 1;
                }
            }
            public bool Val2Enabled
            {
                get
                {
                    return Values.Count > 2;
                }
            }
            public bool Val3Enabled
            {
                get
                {
                    return Values.Count > 3;
                }
            }
        }

        public ObservableCollection<WrappedConstant> Constants;

        private XivMtrl Material;

        private XivMtrl _OriginalMaterial;
        public ShaderConstantsEditor(XivMtrl material)
        {
            Material = (XivMtrl)material.Clone();
            _OriginalMaterial = material;
            InitializeComponent();
            UpdateList();
        }

        private void UpdateList()
        {
            Constants = new ObservableCollection<WrappedConstant>();
            foreach (var c in Material.ShaderConstants)
            {
                Constants.Add(new WrappedConstant(this, (ShaderConstant)c));
            }
            ConstantsList.ItemsSource = Constants;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            _OriginalMaterial.ShaderConstants = Material.ShaderConstants;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        public static bool ShowConstantsEditor(XivMtrl material, Window owner = null)
        {
            var wind = new ShaderConstantsEditor(material);
            wind.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            wind.Owner = owner != null ? owner : System.Windows.Application.Current.MainWindow;
            var result = wind.ShowDialog();
            if(result != true)
            {
                return false;
            }

            return true;
        }
        private void RemoveConstant_Click(object sender, RoutedEventArgs e)
        {
            var con = ((WrappedConstant)((Button)sender).DataContext).Constant;
            Material.ShaderConstants.Remove(con);
            UpdateList();
        }

        private void AddConstant_Click(object sender, RoutedEventArgs e)
        {
            var newConst = new ShaderConstant();

            if (ShaderKeys[Material.Shader].Count > 0)
            {
                var info = ShaderConstants[Material.Shader].First().Value;
                newConst.ConstantId = info.Id;
                newConst.Values = info.DefaultValues;
            }
            Material.ShaderConstants.Add(newConst);
            UpdateList();

        }
    }
}
