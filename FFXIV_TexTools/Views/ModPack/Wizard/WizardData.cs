﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using xivModdingFramework.Cache;
using xivModdingFramework.General;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.FileTypes.PMP;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Variants.DataContainers;
using static xivModdingFramework.Mods.FileTypes.TTMP;

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


        public List<WizardOptionEntry> Options;

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
                    var standardData = new WizardStandardOptionData();
                    var sOp = o as PmpStandardOptionJson;

                    var data = await PMP.UnpackPmpOption(o, null, unzipPath);
                    wizOp.StandardData.Files = data;                   


                    wizOp.StandardData = standardData;
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
    }

    /// <summary>
    /// Class representing a Page of Groups.
    /// </summary>
    public class WizardPageEntry
    {
        public string Name;
        public List<WizardGroupEntry> Groups;

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

    }

    /// <summary>
    /// Class representing the description/cover page of a Modpack.
    /// </summary>
    public class WizardMetaEntry
    {
        public string Name;
        public string Author;
        public string Description;
        public string Url;
        public string Version;

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
        public WizardMetaEntry MetaPage;
        public List<WizardPageEntry> OptionPages;
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
            data.OptionPages = new List<WizardPageEntry>();
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
                    data.OptionPages.Add(await WizardPageEntry.FromPenumbraPage(g, unzipPath));
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

                data.OptionPages.Add(await WizardPageEntry.FromPenumbraPage(fakeGroup, unzipPath));
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

            data.OptionPages = new List<WizardPageEntry>();
            foreach (var p in ttmp.ModPackPages)
            {
                data.OptionPages.Add(await WizardPageEntry.FromWizardModpackPage(p, unzipPath));
            }
            return data;
        }

        /// <summary>
        /// Updates the base Penumbra groups with the new user-selected values.
        /// </summary>
        public void FinalizePmpSelections()
        {
            // Need to go through and assign the Selected values back to the PMP.
            foreach(var p in OptionPages)
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
            foreach (var p in OptionPages)
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