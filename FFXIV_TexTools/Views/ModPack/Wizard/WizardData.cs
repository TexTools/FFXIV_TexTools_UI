using Newtonsoft.Json;
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
using xivModdingFramework.Cache;
using xivModdingFramework.General;
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

    public class WizardStandardOptionData : WizardOptionData
    {
        public Dictionary<string, FileStorageInformation> Files = new Dictionary<string, FileStorageInformation>();

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
        public string ImagePath { get; set; }

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
            if (!string.IsNullOrWhiteSpace(ImagePath))
            {
                img = Image.Load(ImagePath);
            }

            var mo = new ModOption()
            {
                Description = Description,
                Name = Name,
                GroupName = _Group.Name,
                ImageFileName = ImagePath,
                IsChecked = Selected,
                SelectionType = OptionType.ToString(),
                Image = img,
            };

            if(StandardData == null)
            {
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

            return mo;
        }

        public async Task<PMPOptionJson> ToPmpOption(string tempFolder, IEnumerable<FileIdentifier> identifiers)
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
                    wizOp.ImagePath = Path.Combine(unzipPath, o.ImagePath);
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
                    data.Files.Add(mj.FullPath, finfo);
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
            group.UserSelection = pGroup.SelectedSettings > 0 ? pGroup.SelectedSettings : pGroup.DefaultSettings;

            group.OptionType = pGroup.Type == "Single" ? EOptionType.Single : EOptionType.Multi;
            group.Name = pGroup.Name;
            group.Priority = pGroup.Priority;

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
                wizOp.ImagePath = null;

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
                    var data = await PMP.UnpackPmpOption(o, null, unzipPath);
                    wizOp.StandardData.Files = data;
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

        public async Task<PMPGroupJson> ToPmpGroup(string tempFolder, Dictionary<string, List<FileIdentifier>> identifiers)
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

            pg.DefaultSettings = UserSelection;
            pg.Name = Name;
            pg.Description = Description;
            pg.Options = new List<PMPOptionJson>();
            pg.Priority = Priority;
            pg.SelectedSettings = UserSelection;


            foreach(var option in Options)
            {
                var optionPrefix = IOUtil.MakePathSafe(Name) + "/" + IOUtil.MakePathSafe(option.Name);
                identifiers.TryGetValue(optionPrefix, out var files);
                pg.Options.Add(await option.ToPmpOption(tempFolder, files));
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

        public static async Task<WizardPageEntry> FromPenumbraPage(PMPGroupJson pGroup, string unzipPath)
        {
            // Penumbra doesn't actually have pages, just groups.
            var page = new WizardPageEntry();
            page.Name = pGroup.Name;
            page.Groups = new List<WizardGroupEntry>();
            page.Groups.Add(await WizardGroupEntry.FromPMPGroup(pGroup, unzipPath));
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

        public static WizardMetaEntry FromPMP(PMPJson pmp, string unzipPath)
        {
            var meta = pmp.Meta;
            var page = new WizardMetaEntry();
            page.Url = meta.Website;
            page.Version = meta.Version;
            page.Author = meta.Author;
            page.Description = meta.Description;
            page.Name = meta.Name;
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
                foreach (var g in pmp.Groups)
                {
                    data.DataPages.Add(await WizardPageEntry.FromPenumbraPage(g, unzipPath));
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

                data.DataPages.Add(await WizardPageEntry.FromPenumbraPage(fakeGroup, unzipPath));
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

                            var optionPrefix = IOUtil.MakePathSafe(g.Name) + "/" + IOUtil.MakePathSafe(o.Name);
                            allFiles.Add(optionPrefix, files);
                        }
                    }
                }

                // These are de-duplicated internal write paths for the final PMP folder structure, coupled with
                // their file identifier and internal path information
                var identifiers = await FileIdentifier.IdentifierListFromDictionaries(allFiles);


                // This both constructs the JSON structure and writes our files to their
                // real location in the folder tree in the temp folder.
                foreach (var p in DataPages)
                {
                    foreach (var g in p.Groups)
                    {
                        var pg = await g.ToPmpGroup(tempFolder, identifiers);
                        pmp.Groups.Add(pg);
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
                    var pmpGroup = g.ModOption as PMPGroupJson;
                    if(pmpGroup == null)
                    {
                        continue;
                    }

                    var selected = 0;
                    for(int i = 0; i < g.Options.Count; i++)
                    {
                        var opt = g.Options[i];
                        if (opt.Selected)
                        {
                            if(g.OptionType == EOptionType.Single)
                            {
                                selected = i;
                                break;
                            } else
                            {
                                var shifted = 1 << i;
                                selected |= shifted;
                            }
                        }
                    }

                    pmpGroup.SelectedSettings = selected;
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
                            var ttOpt = ttGroup.OptionList[i];
                            modFiles.AddRange(ttOpt.ModsJsons);
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
