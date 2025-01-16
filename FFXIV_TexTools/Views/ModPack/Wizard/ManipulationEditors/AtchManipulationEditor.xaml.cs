using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using xivModdingFramework.Mods.FileTypes;

namespace FFXIV_TexTools.Views.Wizard.ManipulationEditors
{
    /// <summary>
    /// Interaction logic for AtchManipulationEditor.xaml
    /// </summary>
    public partial class AtchManipulationEditor : UserControl, INotifyPropertyChanged
    {
        PMPAtchManipulationJson Manipulation;


        public event PropertyChangedEventHandler PropertyChanged;
        public PMPModelRace Race
        {
            get => Manipulation.Race;
            set
            {
                Manipulation.Race = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Race)));
            }
        }
        public PMPGender Gender
        {
            get => Manipulation.Gender;
            set
            {
                Manipulation.Gender = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Attribute)));
            }
        }
        public string Type
        {
            get => Manipulation.Type;
            set
            {
                Manipulation.Type = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Type)));
            }
        }
        public int Index
        {
            get => Manipulation.Index;
            set
            {
                Manipulation.Index = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Type)));
            }
        }
        public string Bone
        {
            get => Manipulation.Entry.Bone;
            set
            {
                Manipulation.Entry.Bone = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Type)));
            }
        }
        public float Scale
        {
            get => Manipulation.Entry.Scale;
            set
            {
                Manipulation.Entry.Scale = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Type)));
            }
        }
        public float OffsetX
        {
            get => Manipulation.Entry.OffsetX;
            set
            {
                Manipulation.Entry.OffsetX = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Type)));
            }
        }
        public float OffsetY
        {
            get => Manipulation.Entry.OffsetY;
            set
            {
                Manipulation.Entry.OffsetY = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Type)));
            }
        }
        public float OffsetZ
        {
            get => Manipulation.Entry.OffsetZ;
            set
            {
                Manipulation.Entry.OffsetZ = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Type)));
            }
        }
        public float RotationX
        {
            get => Manipulation.Entry.RotationX;
            set
            {
                Manipulation.Entry.RotationX = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Type)));
            }
        }
        public float RotationY
        {
            get => Manipulation.Entry.RotationY;
            set
            {
                Manipulation.Entry.RotationY = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Type)));
            }
        }
        public float RotationZ
        {
            get => Manipulation.Entry.RotationZ;
            set
            {
                Manipulation.Entry.RotationZ = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Type)));
            }
        }

        public ObservableCollection<KeyValuePair<string, PMPModelRace>> Races { get; set; } = ViewHelpers.GetEnumSource<PMPModelRace>();
        public ObservableCollection<KeyValuePair<string, PMPGender>> Genders { get; set; } = ViewHelpers.GetEnumSource<PMPGender>();
        public ObservableCollection<KeyValuePair<string, string>> Types { get; set; } = new()
        {
            // Someone could add descriptive names to the first column here if they wanted to
            new("2ax", "2ax"),
            new("2bk", "2bk"),
            new("2bs", "2bs"),
            new("2bw", "2bw"),
            new("2ff", "2ff"),
            new("2gb", "2gb"),
            new("2gl", "2gl"),
            new("2gn", "2gn"),
            new("2km", "2km"),
            new("2kt", "2kt"),
            new("2kz", "2kz"),
            new("2rp", "2rp"),
            new("2sp", "2sp"),
            new("2st", "2st"),
            new("2sw", "2sw"),
            new("aai", "aai"),
            new("aal", "aal"),
            new("aar", "aar"),
            new("abl", "abl"),
            new("aco", "aco"),
            new("agl", "agl"),
            new("ali", "ali"),
            new("alm", "alm"),
            new("alt", "alt"),
            new("ase", "ase"),
            new("atr", "atr"),
            new("avt", "avt"),
            new("awo", "awo"),
            new("bag", "bag"),
            new("bl2", "bl2"),
            new("bld", "bld"),
            new("bll", "bll"),
            new("brs", "brs"),
            new("bsl", "bsl"),
            new("chk", "chk"),
            new("clg", "clg"),
            new("cls", "cls"),
            new("clw", "clw"),
            new("col", "col"),
            new("cor", "cor"),
            new("cos", "cos"),
            new("crd", "crd"),
            new("crr", "crr"),
            new("crt", "crt"),
            new("csl", "csl"),
            new("csr", "csr"),
            new("dge", "dge"),
            new("dgr", "dgr"),
            new("drm", "drm"),
            new("dur", "dur"),
            new("ebz", "ebz"),
            new("egp", "egp"),
            new("elg", "elg"),
            new("fcb", "fcb"),
            new("fch", "fch"),
            new("fdr", "fdr"),
            new("fha", "fha"),
            new("fl2", "fl2"),
            new("flt", "flt"),
            new("frg", "frg"),
            new("fry", "fry"),
            new("fsh", "fsh"),
            new("fsw", "fsw"),
            new("fud", "fud"),
            new("gdb", "gdb"),
            new("gdh", "gdh"),
            new("gdl", "gdl"),
            new("gdp", "gdp"),
            new("gdr", "gdr"),
            new("gdt", "gdt"),
            new("gdw", "gdw"),
            new("gsl", "gsl"),
            new("gsr", "gsr"),
            new("gun", "gun"),
            new("hel", "hel"),
            new("hmm", "hmm"),
            new("hrp", "hrp"),
            new("htc", "htc"),
            new("ksh", "ksh"),
            new("let", "let"),
            new("lpr", "lpr"),
            new("mlt", "mlt"),
            new("mmc", "mmc"),
            new("mrb", "mrb"),
            new("mrh", "mrh"),
            new("msg", "msg"),
            new("mwp", "mwp"),
            new("ndl", "ndl"),
            new("nik", "nik"),
            new("njd", "njd"),
            new("nph", "nph"),
            new("orb", "orb"),
            new("oum", "oum"),
            new("pen", "pen"),
            new("pic", "pic"),
            new("plt", "plt"),
            new("pra", "pra"),
            new("prc", "prc"),
            new("prf", "prf"),
            new("qvr", "qvr"),
            new("rap", "rap"),
            new("rbt", "rbt"),
            new("rec", "rec"),
            new("rgk", "rgk"),
            new("rgs", "rgs"),
            new("rod", "rod"),
            new("rop", "rop"),
            new("rp1", "rp1"),
            new("saw", "saw"),
            new("sbt", "sbt"),
            new("sca", "sca"),
            new("sci", "sci"),
            new("sht", "sht"),
            new("sic", "sic"),
            new("sld", "sld"),
            new("stf", "stf"),
            new("stv", "stv"),
            new("swd", "swd"),
            new("sxs", "sxs"),
            new("sxw", "sxw"),
            new("syl", "syl"),
            new("syr", "syr"),
            new("syu", "syu"),
            new("syw", "syw"),
            new("tan", "tan"),
            new("tbl", "tbl"),
            new("tcs", "tcs"),
            new("tgn", "tgn"),
            new("tmb", "tmb"),
            new("tms", "tms"),
            new("trm", "trm"),
            new("trr", "trr"),
            new("trw", "trw"),
            new("vln", "vln"),
            new("wcr", "wcr"),
            new("whl", "whl"),
            new("wng", "wng"),
            new("ypd", "ypd"),
            new("yt2", "yt2"),
            new("ytc", "ytc"),
            new("ytk", "ytk"),
        };

        public AtchManipulationEditor(PMPManipulationWrapperJson manipulation)
        {
            var wrapper = manipulation as PMPAtchManipulationWrapperJson;
            Manipulation = wrapper.Manipulation;
            DataContext = this;
            InitializeComponent();
        }
    }
}
