using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.Helpers;
using Vector2 = SharpDX.Vector2;

namespace FFXIV_TexTools.Views.Models
{
    /// <summary>
    /// Interaction logic for ModifyPartWindow.xaml
    /// </summary>
    public partial class ModifyVerticesWindow : MetroWindow, INotifyPropertyChanged
    {
        private object _Element;

        private TTMeshPart Part
        {
            get => _Element as TTMeshPart;
        }
        private TTMeshGroup Mesh
        {
            get => _Element as TTMeshGroup;
        }
        private TTModel Model
        {
            get => _Element as TTModel;
        }

        private TTModel _RawModel;

        private Timer _Timer;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<KeyValuePair<string, Vector2>> FlowSource { get; set; } = new ObservableCollection<KeyValuePair<string, Vector2>>()
        {
            new KeyValuePair<string, Vector2>("Vertical |", new Vector2(0,1)),
            new KeyValuePair<string, Vector2>("Horizontal |", new Vector2(1,0)),
            new KeyValuePair<string, Vector2>("Diagonal \\", new Vector2(1,1)),
            new KeyValuePair<string, Vector2>("Diagonal /", new Vector2(1,-1)),
        };

        private Vector2 _FlowDirection = new Vector2(0,1);
        public Vector2 FlowDataDirection
        {
            get
            {
                return _FlowDirection;
            }
            set
            {
                _FlowDirection = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlowDataDirection)));
            }
        }

        public ModifyVerticesWindow(object element, TTModel model)
        {
            _RawModel = model;
            DataContext = this;
            _Element = element;
            InitializeComponent();
            Closing += OnClose;
            NoticeLabel.Content = "";

            if(Part != null)
            {
                Title = "Modify Part Vertices: " + Part.Name;
            } else if(Mesh != null)
            {
                Title = "Modify Mesh Vertices: " + Mesh.Name;
            } else if (Model != null)
            {
                if (string.IsNullOrWhiteSpace(Model.Source))
                {
                    Title = "Modify Model Vertices: Unknown Model";
                }
                else
                {
                    Title = "Modify Model Vertices: " + Path.GetFileNameWithoutExtension(Model.Source);
                }
            }
            else
            {
                this.ShowWarning("Invalid Object", "Cannot edit invalid object: " + element);
                this.Close();
            }
        }

        private void OnClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Owner?.Activate();
        }

        public static void ShowVertexModifier(object element, TTModel model, Window owner)
        {
            var wind = new ModifyVerticesWindow(element, model);
            wind.Owner = owner;
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            wind.ShowDialog();
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SetNotice(string text, bool err = false)
        {
            if(_Timer != null)
            {
                _Timer.Stop();
                _Timer.Elapsed -= _Timer_Elapsed;
            }

            if(err)
            {
                NoticeLabel.Foreground = Brushes.DarkRed;
            } else
            {
                NoticeLabel.Foreground = Brushes.DarkGreen;
            }
            NoticeLabel.Content = text;
            _Timer = new Timer(3000);
            _Timer.Start();
            _Timer.Elapsed += _Timer_Elapsed;
            
        }

        private void _Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() => { 
                NoticeLabel.Content = "";
            });
        }

        private async void ClearUv2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ForAllParts(async (p) =>
                {
                    ModelModifiers.ClearUV2_Part(p);
                });
                SetNotice("UV2 Cleared");
            }
            catch (Exception ex)
            {
                SetNotice("Unable to Clear UV2: " + ex.Message, true);
            }
        }
        private async void CopyUv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ForAllParts(async (p) =>
                {
                    ModelModifiers.CloneUV2_Part(p);
                });
                SetNotice("UV1 Copied to UV2");
            }
            catch (Exception ex)
            {
                SetNotice("Unable to Copy UV: " + ex.Message, true);
            }
        }
        private async void ClearVColor1_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ForAllParts(async (p) =>
                {
                    ModelModifiers.ClearVColor_Part(p);
                });
                SetNotice("Vertex Color 1 Cleared");
            }
            catch (Exception ex)
            {
                SetNotice("Unable to Clear VColor1: " + ex.Message, true);
            }
        }
        private async void ClearVColor2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ForAllParts(async (p) =>
                {
                    ModelModifiers.ClearVColor2_Part(p);
                });
                SetNotice("Vertex Color 2 Cleared");
            }
            catch (Exception ex)
            {
                SetNotice("Unable to Clear VColor2: " + ex.Message, true);
            }
        }
        private async void ClearVAlpha_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ForAllParts(async (p) =>
                {
                    ModelModifiers.ClearVAlpha_Part(p);
                });
                SetNotice("Vertex Alpha Cleared");
            } catch(Exception ex)
            {
                SetNotice("Unable to Clear Vertex Alpha: " + ex.Message, true);
            }
        }


        private async Task ForAllParts(Func<TTMeshPart, Task> act)
        {
            var tasks = new List<Task>();
            if(Part != null)
            {
                tasks.Add(Task.Run(async() => await act(Part)));
            } else if(Mesh != null) {
                foreach(var p in Mesh.Parts)
                {
                    tasks.Add(Task.Run(async () => await act(p)));
                }
            } else if(Model != null)
            {
                foreach(var m in Model.MeshGroups)
                {
                    foreach(var p in m.Parts)
                    {
                        tasks.Add(Task.Run(async () => await act(p)));
                    }
                }
            }

            await Task.WhenAll(tasks);
        }

        private async void ClearFlow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ForAllParts(async (p) =>
                {
                    ModelModifiers.ClearFlow_Part(p);
                });
                _RawModel.AnisotropicLightingEnabled = true;
                SetNotice("Flow Data Cleared");
            }
            catch (Exception ex)
            {
                SetNotice("Unable to Clear Flow Data: " + ex.Message, true);
            }
        }

        private async void GenerateFlow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dir = FlowDataDirection;
                await ForAllParts(async (p) =>
                {
                    ModelModifiers.SetFlow_Part(p, dir);
                });
                _RawModel.AnisotropicLightingEnabled = true;
                SetNotice("Flow Data Generated");
            }
            catch (Exception ex)
            {
                SetNotice("Unable to Generate Flow Data: " + ex.Message, true);
            }
        }

        private void DisableFlow_Click(object sender, RoutedEventArgs e)
        {
            _RawModel.AnisotropicLightingEnabled = false;
            SetNotice("Flow Data Disabled");
        }
    }
}
