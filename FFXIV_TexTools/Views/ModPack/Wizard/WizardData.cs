using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml.Linq;
using xivModdingFramework.Cache;
using xivModdingFramework.General;
using xivModdingFramework.General.DataContainers;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.Mods.FileTypes.PMP;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Variants.DataContainers;
using static xivModdingFramework.Mods.FileTypes.TTMP;
using Image = SixLabors.ImageSharp.Image;

// ================================ //

/// This file contains all of the Data Classes for the Advanced Modpack Wizard, both import and export.
/// This, thus, also contains all of the logic necessary for generating these classes from the 
/// TTMP or PMP Json formats, and converting them back into those formats/into TTMP/PMP files.
/// 
/// This borders a little on being too heavy to maintain as a TexTools class, but inevitably the
/// UI Model in this case are very close to the final product by definition, and difficult to break
/// apart without adding layers of inefficiency and redundancy that would make maintaining them even worse.

// ================================ //

namespace FFXIV_TexTools.Views.Wizard
{
    public enum EOptionType
    {
        Single,
        Multi
    };

    public enum EGroupType
    {
        Standard,
        Imc
    };

    internal static class WizardHelpers
    {
        public static string WriteImage(string currentPath, string tempFolder, string newName) {

            if (string.IsNullOrWhiteSpace(currentPath))
            {
                return "";
            }

            if (!File.Exists(currentPath))
            {
                return "";
            }

            var path = "images/" + newName + ".png";

            var img = SixLabors.ImageSharp.Image.Load(currentPath);
            var fName = Path.Combine(tempFolder, path);
            var dir = Path.GetDirectoryName(fName);
            Directory.CreateDirectory(dir);

            using var fs = File.OpenWrite(fName);
            var enc = new PngEncoder();
            enc.BitDepth = PngBitDepth.Bit16;
            img.Save(fs, enc);

            return path;
        }

    }

    public class WizardStandardOptionData : WizardOptionData
    {
        public Dictionary<string, FileStorageInformation> Files = new Dictionary<string, FileStorageInformation>();

        public List<PMPManipulationWrapperJson> Manipulations = new List<PMPManipulationWrapperJson>();

        protected override bool HasData()
        {
            return Files.Count > 0;
        }
    }

    public class WizardImcOptionData : WizardOptionData
    {
        public bool IsDisableOption;
        public ushort AttributeMask;

        protected override bool HasData()
        {
            return true;
        }
    }

    public class WizardOptionData
    {

        public bool AnyData
        {
            get
            {
                return HasData();
            }
        }

        protected virtual bool HasData()
        {
            return false;
        }

    }

    /// <summary>
    /// Class representing a single, clickable [Option],
    /// Aka a Radio Button or Checkbox the user can select, that internally resolves
    /// to a single list of files to be imported.
    /// </summary>
    public class WizardOptionEntry : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Image { get; set; }

        private bool _Selected;
        public bool Selected
        {
            get
            {
                return _Selected;
            }
            set
            {
                _Selected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));

                var index = _Group.Options.IndexOf(this);
                if (index < 0) return;

                if(OptionType == EOptionType.Single)
                {
                    if (_Selected)
                    {
                        _Group.UserSelection = index;
                    }
                } else
                {
                    var mask = 1 << index;
                    if (_Selected)
                    {
                        _Group.UserSelection |= mask;
                    }
                    else
                    {
                        _Group.UserSelection &= ~mask;
                    }
                }
            }
        }

        private WizardGroupEntry _Group;

        // Group name is used by the UI template binding for establishing radio button groupings.
        public string GroupName
        {
            get
            {
                return _Group.Name;
            }
        }

        // Option type is used by the UI template binding to determine template type.
        public EOptionType OptionType
        {
            get
            {
                return _Group.OptionType;
            }
        }
        public EGroupType GroupType
        {
            get
            {
                return _Group.GroupType;
            }
        }

        private WizardOptionData _Data = new WizardStandardOptionData();

        public WizardImcOptionData ImcData
        {
            get
            {
                if(GroupType == EGroupType.Imc)
                {
                    return _Data as WizardImcOptionData;
                } else
                {
                    return null;
                }
            }
            set
            {
                if(GroupType == EGroupType.Imc)
                {
                    _Data = value;
                }
            }
        }

        public WizardStandardOptionData StandardData
        {
            get
            {
                if (GroupType == EGroupType.Standard)
                {
                    return _Data as WizardStandardOptionData;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (GroupType == EGroupType.Standard)
                {
                    _Data = value;
                }
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        public WizardOptionEntry(WizardGroupEntry owningGroup)
        {
            _Group = owningGroup;
        }

        public async Task<ModOption> ToModOption()
        {
            Image img = null;
            if (!string.IsNullOrWhiteSpace(Image))
            {
                img = SixLabors.ImageSharp.Image.Load(Image);
            }

            var mo = new ModOption()
            {
                Description = Description,
                Name = Name,
                GroupName = _Group.Name,
                ImageFileName = Image,
                IsChecked = Selected,
                SelectionType = OptionType.ToString(),
                Image = img,
            };

            if(StandardData == null)
            {
                throw new NotImplementedException();
                return mo;
            }

            foreach(var fkv in StandardData.Files)
            {
                var path = fkv.Key;
                var forceType2 = path.EndsWith(".atex");
                if (!File.Exists(fkv.Value.RealPath))
                {
                    // Sometimes poorly behaved PMPs or Penumbra folders may have been used as a source,
                    // where they are missing files that they claim to have.
                    continue;
                }
                var data = await TransactionDataHandler.GetCompressedFile(fkv.Value, forceType2);

                var root = await XivCache.GetFirstRoot(path);
                var itemCategory = "Unknown";
                var itemName = "Unknown";
                if(root != null)
                {
                    var item = root.GetFirstItem();
                    if(item != null)
                    {
                        itemCategory = item.SecondaryCategory;
                        itemName = item.Name;
                    }
                }

                var mData = new ModData()
                {
                    Name = itemName,
                    Category = itemCategory,
                    FullPath = path,
                    ModDataBytes = data,
                };
                mo.Mods.Add(path, mData);
            }

            if (StandardData.Manipulations != null && StandardData.Manipulations.Count > 0) {
                // Readonly TX for retrieving base values.
                var tx = ModTransaction.BeginTransaction();
                var manips = await PMP.ManipulationsToMetadata(this.StandardData.Manipulations, tx);

                foreach(var meta in manips.Metadatas)
                {
                    // Need to convert these and add them to the file array.
                    var item = meta.Root.GetFirstItem();
                    var path = meta.Root.Info.GetRootFile();
                    var mData = new ModData()
                    {
                        Name = item.Name,
                        Category = item.SecondaryCategory,
                        FullPath = path,
                        ModDataBytes = await ItemMetadata.Serialize(meta),
                    };
                    mo.Mods.Add(path, mData);
                }

                foreach(var rgsp in manips.Rgsps)
                {
                    // Need to convert these and add them to the file array.
                    var data = rgsp.GetBytes();
                    var path = CMP.GetRgspPath(rgsp);
                    var item = CMP.GetDummyItem(rgsp);

                    var mData = new ModData()
                    {
                        Name = item.Name,
                        Category = item.SecondaryCategory,
                        FullPath = path,
                        ModDataBytes = data,
                    };
                    mo.Mods.Add(path, mData);
                }
            }


            return mo;
        }

        public async Task<PMPOptionJson> ToPmpOption(string tempFolder, IEnumerable<FileIdentifier> identifiers, string imageName)
        {
            PMPOptionJson op;
            if(GroupType == EGroupType.Imc)
            {
                if (ImcData.IsDisableOption)
                {
                    var io = new PmpDisableImcOptionJson();
                    op = io;
                    io.IsDisableSubMod = true;
                } else
                {
                    var io = new PmpImcOptionJson();
                    op = io;
                    io.AttributeMask = ImcData.AttributeMask;
                }
                op.Name = Name;
                op.Description = Description;
            } else
            {
                // This unpacks our deduplicated files as needed.
                op = await PMP.CreatePmpStandardOption(tempFolder, Name, Description, identifiers);
            }

            op.Image = WizardHelpers.WriteImage(Image, tempFolder, imageName);


            return op;
        }
    }


    public class WizardImcGroupData
    {
        public XivDependencyRoot Root;
        public ushort Variant;
        public XivImc BaseEntry;
    }

    /// <summary>
    /// Class represnting a Group of options.
    /// Aka a collection of radio buttons or checkboxes.
    /// </summary>
    public class WizardGroupEntry
    {
        public string Name;
        public string Description;
        public string Image;

        // Int or Bitflag depending on OptionType.
        public int DefaultSelection;

        // Int or Bitflag depending on OptionType.
        public int UserSelection;

        public EOptionType OptionType;

        public EGroupType GroupType
        {
            get
            {
                if(ImcData != null)
                {
                    return EGroupType.Imc;
                }
                return EGroupType.Standard;
            }
        }


        public List<WizardOptionEntry> Options = new List<WizardOptionEntry>();

        /// <summary>
        /// Option Data for Penumbra style Imc-Mask Option Groups.
        /// </summary>
        public WizardImcGroupData ImcData = null;

        public int Priority;

        /// <summary>
        /// Handler to the base modpack option.
        /// Typically either a ModGroupJson or PMPGroupJson
        /// </summary>
        public object ModOption;

        public static async Task<WizardGroupEntry> FromWizardGroup(ModGroupJson tGroup, string unzipPath)
        {
            var group = new WizardGroupEntry();
            group.Options = new List<WizardOptionEntry>();
            group.ModOption = tGroup;

            group.Name = tGroup.GroupName;
            group.OptionType = tGroup.SelectionType == "Single" ? EOptionType.Single : EOptionType.Multi;

            var mpdPath = Path.Combine(unzipPath, "TTMPD.mpd");

            foreach(var o in tGroup.OptionList)
            {
                var wizOp = new WizardOptionEntry(group);
                wizOp.Name = o.Name;
                wizOp.Description = o.Description;
                if(!String.IsNullOrWhiteSpace(o.ImagePath))
                {
                    wizOp.Image = Path.Combine(unzipPath, o.ImagePath);
                }
                wizOp.Selected = o.IsChecked;
                
                var data = new WizardStandardOptionData();

                foreach(var mj in o.ModsJsons)
                {
                    var finfo = new FileStorageInformation()
                    {
                        StorageType = EFileStorageType.CompressedBlob,
                        FileSize = mj.ModSize,
                        RealOffset = mj.ModOffset,
                        RealPath = mpdPath
                    };
                    if (mj.FullPath.EndsWith(".meta") || mj.FullPath.EndsWith(".rgsp"))
                    {
                        var raw = await TransactionDataHandler.GetUncompressedFile(finfo);
                        if (mj.FullPath.EndsWith(".meta"))
                        {
                            var meta = await ItemMetadata.Deserialize(raw);
                            data.Manipulations.AddRange(PMPExtensions.MetadataToManipulations(meta));
                        }
                        else {
                            var rgsp = new RacialGenderScalingParameter(raw);
                            data.Manipulations.AddRange(PMPExtensions.RgspToManipulations(rgsp));
                        }

                    }
                    else
                    {
                        data.Files.Add(mj.FullPath, finfo);
                    }
                }

                wizOp.StandardData = data;



                group.Options.Add(wizOp);
            }

            if(group.Options.Count == 0)
            {
                // Empty group.
                return null;
            }

            if(group.OptionType == EOptionType.Single && !group.Options.Any(x => x.Selected))
            {
                group.Options[0].Selected = true;
            }

            return group;
        }

        public static async Task<WizardGroupEntry> FromPMPGroup(PMPGroupJson pGroup, string unzipPath)
        {
            var group = new WizardGroupEntry();
            group.Options = new List<WizardOptionEntry>();
            group.ModOption = pGroup;
            group.DefaultSelection = pGroup.DefaultSettings;
            group.UserSelection = pGroup.DefaultSettings;

            group.OptionType = pGroup.Type == "Single" ? EOptionType.Single : EOptionType.Multi;
            group.Name = pGroup.Name;
            group.Priority = pGroup.Priority;
            
            if (!string.IsNullOrWhiteSpace(pGroup.Image))
            {
                group.Image = Path.Combine(unzipPath, pGroup.Image);
            } else
            {
                group.Image = "";
            }

            group.Description = pGroup.Description;

            var imcGroup = pGroup as PMPImcGroupJson;
            if (imcGroup != null)
            {
                group.ImcData = new WizardImcGroupData()
                {
                    Variant = imcGroup.Identifier.Variant,
                    Root = imcGroup.Identifier.GetRoot(),
                    BaseEntry = imcGroup.DefaultEntry.ToXivImc(),
                };
            }

            var idx = 0;
            foreach(var o in pGroup.Options)
            {
                var wizOp = new WizardOptionEntry(group);
                wizOp.Name = o.Name;
                wizOp.Description = o.Description;
                wizOp.Image = null;

                if (group.OptionType == EOptionType.Single)
                {
                    wizOp.Selected = group.UserSelection == idx;
                }
                else
                {
                    var bit = 1 << idx;
                    wizOp.Selected = (group.UserSelection & bit) > 0;
                }
                group.Options.Add(wizOp);

                if(group.GroupType == EGroupType.Standard)
                {
                    var data = await PMP.UnpackPmpOption(o, null, unzipPath, false);
                    wizOp.StandardData.Files = data.Files;
                    wizOp.StandardData.Manipulations = data.OtherManipulations;

                } else if(group.GroupType == EGroupType.Imc)
                {
                    var imcData = new WizardImcOptionData();
                    var imcOp = o as PmpImcOptionJson;
                    if (imcOp != null)
                    {
                        imcData.IsDisableOption = false;
                        imcData.AttributeMask = imcOp.AttributeMask;
                    }
                    var defOp = o as PmpDisableImcOptionJson;
                    if(defOp != null)
                    {
                        imcData.IsDisableOption = defOp.IsDisableSubMod;
                        imcData.AttributeMask = 0;
                    }
                    wizOp.ImcData = imcData;
                }

                if (!string.IsNullOrWhiteSpace(o.Image))
                {
                    wizOp.Image = Path.Combine(unzipPath, o.Image);
                }
                else
                {
                    wizOp.Image = "";
                }

                idx++;
            }

            if (group.Options.Count == 0)
            {
                // Empty group.
                return null;
            }

            if (group.OptionType == EOptionType.Single && !group.Options.Any(x => x.Selected))
            {
                group.Options[0].Selected = true;
            }


            return group;
        }

        public async Task<ModGroup> ToModGroup()
        {
            if(this.ImcData != null)
            {
                throw new InvalidDataException("TTMP Does not support IMC Selection Groups.");
            }

            var mg = new ModGroup()
            {
                GroupName = Name,
                OptionList = new List<ModOption>(),
                SelectionType = OptionType.ToString(),
            };

            foreach(var option in Options)
            {
                var tOpt = await option.ToModOption();
                mg.OptionList.Add(tOpt);
            }

            return mg;
        }

        public async Task<PMPGroupJson> ToPmpGroup(string tempFolder, Dictionary<string, List<FileIdentifier>> identifiers, int page, bool oneOption = false)
        {
            var pg = new PMPGroupJson();

            if (this.ImcData != null)
            {
                var imcG = new PMPImcGroupJson();
                pg = imcG;
                pg.Type = "Imc";

                // Need to implement identifier and default values here.

                // From ImcData.Root + ImcData.Variant
                //imcG.Identifier = null; 

                // From ImcData.BaseEntry
                //imcG.DefaultEntry = new PMPImcManipulationJson.PMPImcEntry();

                throw new NotImplementedException("Writing IMC Groups not yet implemented.");
            }
            else
            {
                pg.Type = OptionType.ToString();
            }

            pg.Name = Name;
            pg.Description = Description;
            pg.Options = new List<PMPOptionJson>();
            pg.Priority = Priority;
            pg.SelectedSettings = UserSelection;
            pg.DefaultSettings = UserSelection;
            pg.Page = page;

            pg.Image = WizardHelpers.WriteImage(Image, tempFolder, IOUtil.MakePathSafe(Name));

            foreach (var option in Options)
            {
                var optionPrefix = IOUtil.MakePathSafe(Name) + "/" + IOUtil.MakePathSafe(option.Name) + "/";
                var imgName = optionPrefix.Substring(0, optionPrefix.Length - 1);
                if (oneOption)
                {
                    optionPrefix = "";
                    imgName = "default_image";
                }

                identifiers.TryGetValue(optionPrefix, out var files);
                var opt = await option.ToPmpOption(tempFolder, files, imgName);
                var so = opt as PmpStandardOptionJson;
                if (option.StandardData.Manipulations != null)
                {
                    foreach (var m in option.StandardData.Manipulations)
                    {
                        // Carry our extra manipulations through.
                        so.Manipulations.Add(m);
                    }
                }
                pg.Options.Add(opt);
            }

            return pg;
        }
    }

    /// <summary>
    /// Class representing a Page of Groups.
    /// </summary>
    public class WizardPageEntry
    {
        public string Name;
        public List<WizardGroupEntry> Groups = new List<WizardGroupEntry>();

        public static async Task<WizardPageEntry> FromWizardModpackPage(ModPackPageJson jp, string unzipPath)
        {
            var page = new WizardPageEntry();
            page.Name = "Page " + (jp.PageIndex + 1);

            page.Groups = new List<WizardGroupEntry>();
            foreach(var p in jp.ModGroups)
            {
                page.Groups.Add(await WizardGroupEntry.FromWizardGroup(p, unzipPath));
            }
            return page;
        }

        public async Task<ModPackData.ModPackPage> ToModPackPage(int index)
        {
            var mpp = new ModPackData.ModPackPage() { 
                PageIndex = index,
                ModGroups = new List<ModGroup>(),
            };

            foreach(var group in Groups)
            {
                var mpg = await group.ToModGroup();
                mpp.ModGroups.Add(mpg);
            }

            return mpp;
        }
    }

    /// <summary>
    /// Class representing the description/cover page of a Modpack.
    /// </summary>
    public class WizardMetaEntry
    {
        public string Name = "";
        public string Author = "";
        public string Description = "";
        public string Url = "";
        public string Version = "1.0";
        public string Image = "";

        public static WizardMetaEntry FromPMP(PMPJson pmp, string unzipPath)
        {
            var meta = pmp.Meta;
            var page = new WizardMetaEntry();
            page.Url = meta.Website;
            page.Version = meta.Version;
            page.Author = meta.Author;
            page.Description = meta.Description;
            page.Name = meta.Name;
            if (!string.IsNullOrWhiteSpace(meta.Image))
            {
                page.Image = Path.Combine(unzipPath, meta.Image);
            }
            else
            {
                page.Image = "";
            }
            return page;
        }

        public static WizardMetaEntry FromWizardModpack(ModPackJson wiz, string unzipPath)
        {
            var page = new WizardMetaEntry();
            page.Url = wiz.Url;
            page.Name = wiz.Name;
            page.Version = wiz.Version;
            page.Author = wiz.Author;
            page.Description = wiz.Description;
            return page;
        }
    }


    /// <summary>
    /// The full set of data necessary to render and display a wizard modpack install.
    /// </summary>
    public class WizardData
    {
        public WizardMetaEntry MetaPage = new WizardMetaEntry();
        public List<WizardPageEntry> DataPages = new List<WizardPageEntry>();
        public EModpackType ModpackType;
        public ModPack ModPack;

        /// <summary>
        /// Original source this Wizard Data was generated from.
        /// Null if the user created a fresh modpack in the UI.
        /// </summary>
        public object RawSource;

        public static async Task<WizardData> FromPmp(PMPJson pmp, string unzipPath)
        {
            var data = new WizardData();
            data.MetaPage = WizardMetaEntry.FromPMP(pmp, unzipPath);
            data.DataPages = new List<WizardPageEntry>();
            data.ModpackType = EModpackType.Pmp;

            var mp = new ModPack(null);
            mp.Author = data.MetaPage.Author;
            mp.Version = data.MetaPage.Version;
            mp.Name = data.MetaPage.Name;
            mp.Url = data.MetaPage.Url;
            data.ModPack = mp;
            data.RawSource = pmp;

            if (pmp.Groups.Count > 0)
            {
                // Create sufficient pages.
                var pageMax = pmp.Groups.Max(x => x.Page);
                for (int i = 0; i <= pageMax; i++)
                {
                    var page = new WizardPageEntry();
                    page.Name = "Page " + (i+1).ToString();
                    page.Groups = new List<WizardGroupEntry>();
                    data.DataPages.Add(page);
                }

                // Assign groups to pages.
                foreach (var g in pmp.Groups)
                {
                    var page = data.DataPages[g.Page];
                    page.Groups.Add(await WizardGroupEntry.FromPMPGroup(g, unzipPath));
                }
            } else
            {

                // Just drum up a basic group containing the default option.
                var fakeGroup = new PMPGroupJson();
                fakeGroup.Name = "Default";
                fakeGroup.Options = new List<PMPOptionJson>() { pmp.DefaultMod };
                fakeGroup.SelectedSettings = 1;
                fakeGroup.Type = "Single";

                if (string.IsNullOrWhiteSpace(pmp.DefaultMod.Name))
                {
                    pmp.DefaultMod.Name = "Default";
                }

                var page = new WizardPageEntry();
                page.Name = "Page 1";
                page.Groups = new List<WizardGroupEntry>();
                page.Groups.Add(await WizardGroupEntry.FromPMPGroup(fakeGroup, unzipPath));
                data.DataPages.Add(page);
            }
            return data;
        }

        public static async Task<WizardData> FromWizardPack(ModPackJson ttmp, string unzipPath)
        {
            var data = new WizardData();
            data.ModpackType = EModpackType.TtmpWizard;
            data.MetaPage = WizardMetaEntry.FromWizardModpack(ttmp, unzipPath);

            var mp = new ModPack(null);
            mp.Author = data.MetaPage.Author;
            mp.Version = data.MetaPage.Version;
            mp.Name = data.MetaPage.Name;
            mp.Url = data.MetaPage.Url;
            data.ModPack = mp;
            data.RawSource = ttmp;

            data.DataPages = new List<WizardPageEntry>();
            foreach (var p in ttmp.ModPackPages)
            {
                data.DataPages.Add(await WizardPageEntry.FromWizardModpackPage(p, unzipPath));
            }
            return data;
        }

        public async Task WriteWizardPack(string targetPath)
        {
            Version.TryParse(MetaPage.Version, out var ver);

            ver ??= new Version("1.0");
            var modPackData = new ModPackData()
            {
                Name = MetaPage.Name,
                Author = MetaPage.Author,
                Url = MetaPage.Url,
                Version = ver,
                Description = MetaPage.Description,
                ModPackPages = new List<ModPackData.ModPackPage>(),
            };

            int i = 0;
            foreach(var page in DataPages)
            {
                modPackData.ModPackPages.Add(await page.ToModPackPage(i));
                i++;
            }

            await TTMP.CreateWizardModPack(modPackData, targetPath, null, true);
        }

        public async Task WritePmp(string targetPath)
        {
            var pmp = new PMPJson()
            {
                DefaultMod = new PMPOptionJson(),
                Groups = new List<PMPGroupJson>(),
                Meta = new PMPMetaJson(),
            };

            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);
            try
            {
                Version.TryParse(MetaPage.Version, out var ver);
                ver ??= new Version("1.0");

                pmp.Meta.Name = MetaPage.Name;
                pmp.Meta.Author = MetaPage.Author;
                pmp.Meta.Website = MetaPage.Url;
                pmp.Meta.Description = MetaPage.Description;
                pmp.Meta.Version = ver.ToString();
                pmp.Meta.Tags = new List<string>();
                pmp.Meta.FileVersion = PMP._WriteFileVersion;
                pmp.Meta.Image = WizardHelpers.WriteImage(MetaPage.Image, tempFolder, "_MetaImage");

                var optionCount = DataPages.Sum(p => p.Groups.Sum(x => x.Options.Count));

                // We need to compose a list of all the file storage information we're going to use.
                // Grouped by option folder.
                var allFiles = new Dictionary<string, Dictionary<string, FileStorageInformation>>();
                foreach(var p in DataPages)
                {
                    foreach(var g in p.Groups)
                    {
                        foreach(var o in g.Options)
                        {
                            if(o.GroupType != EGroupType.Standard)
                            {
                                continue;
                            }

                            var files = o.StandardData.Files;

                            if(string.IsNullOrWhiteSpace(o.Name) || string.IsNullOrWhiteSpace(g.Name))
                            {
                                throw new InvalidDataException("PMP Files must have valid group and option names.");
                            }

                            var optionPrefix = IOUtil.MakePathSafe(g.Name) + "/" + IOUtil.MakePathSafe(o.Name) + "/";

                            if(optionCount == 1)
                            {
                                optionPrefix = "";
                            }

                            allFiles.Add(optionPrefix, files);
                        }
                    }
                }

                // These are de-duplicated internal write paths for the final PMP folder structure, coupled with
                // their file identifier and internal path information
                var identifiers = await FileIdentifier.IdentifierListFromDictionaries(allFiles);

                if(optionCount == 1)
                {
                    pmp.DefaultMod = (await DataPages.First(x => x.Groups.Count > 0).Groups.First(x => x.Options.Count > 0).ToPmpGroup(tempFolder, identifiers, 0, true)).Options[0];
                } else
                {
                    // This both constructs the JSON structure and writes our files to their
                    // real location in the folder tree in the temp folder.
                    var page = 0;
                    foreach (var p in DataPages)
                    {
                        foreach (var g in p.Groups)
                        {
                            var pg = await g.ToPmpGroup(tempFolder, identifiers, page);
                            pmp.Groups.Add(pg);
                        }
                        page++;
                    }
                }


                // This performs the final json serialization/writing and zipping.
                await PMP.WritePmp(pmp, tempFolder, targetPath);
            }
            finally
            {
                IOUtil.DeleteTempDirectory(tempFolder);
            }
        }

        /// <summary>
        /// Updates the base Penumbra groups with the new user-selected values.
        /// </summary>
        public void FinalizePmpSelections()
        {
            // Need to go through and assign the Selected values back to the PMP.
            foreach(var p in DataPages)
            {
                foreach(var g in p.Groups)
                {
                    var pg = (g.ModOption as PMPGroupJson);
                    pg.SelectedSettings = g.UserSelection;
                }
            }
        }


        /// <summary>
        /// Returns the list of selected mod files that the TTMP importers expect, based on user selection(s).
        /// </summary>
        /// <returns></returns>
        public List<ModsJson> FinalizeTttmpSelections()
        {
            List<ModsJson> modFiles = new List<ModsJson>();
            // Need to go through and compile the final ModJson list.
            foreach (var p in DataPages)
            {
                foreach (var g in p.Groups)
                {
                    var ttGroup = g.ModOption as ModGroupJson;
                    if (ttGroup == null)
                    {
                        continue;
                    }

                    var selected = 0;
                    for (int i = 0; i < g.Options.Count; i++)
                    {
                        var opt = g.Options[i];
                        if (opt.Selected)
                        {
                            if (opt.GroupType == EGroupType.Standard)
                            {
                                if (opt.StandardData.Manipulations != null && opt.StandardData.Manipulations.Count > 0)
                                {
                                    // We shouldn't actually be able to get to this path, but safety is good.
                                    throw new NotImplementedException("Importing TTMPs with Meta Manipulations is not supported.  How did you get here though?");
                                }

                                var ttOpt = ttGroup.OptionList[i];
                                modFiles.AddRange(ttOpt.ModsJsons);
                            } else
                            {
                                // We shouldn't actually be able to get to this path, but safety is good.
                                throw new NotImplementedException("Importing TTMPs with IMC Groups is not supported.  How did you get here though?");
                            }
                        }
                    }
                }
            }

            // Assign mod pack linkage that the framework expects.
            foreach(var mj in modFiles)
            {
                mj.ModPackEntry = ModPack;
            }

            return modFiles;
        }
    }
}
