using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xivModdingFramework.General;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.FileTypes.PMP;
using static xivModdingFramework.Mods.FileTypes.TTMP;

namespace FFXIV_TexTools.Views.Wizard
{
    public enum EOptionType
    {
        Single,
        Multi
    };

    public class WizardOptionDisplay : INotifyPropertyChanged
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

        private WizardOptionGroup _Group;

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

        public event PropertyChangedEventHandler PropertyChanged;

        public WizardOptionDisplay(WizardOptionGroup owningGroup)
        {
            _Group = owningGroup;
        }
    }

    /// <summary>
    /// Class representing a single selectable option by the end user.
    /// </summary>
    public class WizardOptionGroup
    {
        public string Name;
        public string Description;

        // Int or Bitflag depending on OptionType.
        public int DefaultSelection;

        // Int or Bitflag depending on OptionType.
        public int UserSelection;

        public EOptionType OptionType;

        public List<WizardOptionDisplay> Options;

        /// <summary>
        /// Handler to the base modpack option.
        /// Typically either a ModGroupJson or PMPGroupJson
        /// </summary>
        public object ModOption;

        public static WizardOptionGroup FromWizardGroup(ModGroupJson tGroup, string unzipPath)
        {
            var group = new WizardOptionGroup();
            group.Options = new List<WizardOptionDisplay>();
            group.ModOption = tGroup;

            group.Name = tGroup.GroupName;
            group.OptionType = tGroup.SelectionType == "Single" ? EOptionType.Single : EOptionType.Multi;

            foreach(var o in tGroup.OptionList)
            {
                var wizOp = new WizardOptionDisplay(group);
                wizOp.Name = o.Name;
                wizOp.Description = o.Description;
                if(!String.IsNullOrWhiteSpace(o.ImagePath))
                {
                    wizOp.ImagePath = Path.Combine(unzipPath, o.ImagePath);
                }
                wizOp.Selected = o.IsChecked;
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

        public static WizardOptionGroup FromPMPGroup(PMPGroupJson pGroup, string unzipPath)
        {
            var group = new WizardOptionGroup();
            group.Options = new List<WizardOptionDisplay>();
            group.ModOption = pGroup;
            group.DefaultSelection = pGroup.DefaultSettings;
            group.UserSelection = pGroup.SelectedSettings > 0 ? pGroup.SelectedSettings : pGroup.DefaultSettings;

            group.OptionType = pGroup.Type == "Single" ? EOptionType.Single : EOptionType.Multi;
            group.Name = pGroup.Name;

            group.Description = pGroup.Description;

            var idx = 0;
            foreach(var o in pGroup.Options)
            {
                var wizOp = new WizardOptionDisplay(group);
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
    /// Class representing a user-facing page in the mod wizard.
    /// Largely just a list of the options/groups on that page.
    /// </summary>
    public class WizardOptionsPage
    {
        public string Name;
        public List<WizardOptionGroup> Groups;

        public static WizardOptionsPage FromWizardModpackPage(ModPackPageJson jp, string unzipPath)
        {
            var page = new WizardOptionsPage();
            page.Name = "Page " + (jp.PageIndex + 1);

            page.Groups = new List<WizardOptionGroup>();
            foreach(var p in jp.ModGroups)
            {
                page.Groups.Add(WizardOptionGroup.FromWizardGroup(p, unzipPath));
            }
            return page;
        }

        public static WizardOptionsPage FromPenumbraPage(PMPGroupJson pGroup, string unzipPath)
        {
            // Penumbra doesn't actually have pages, just groups.
            var page = new WizardOptionsPage();
            page.Name = pGroup.Name;
            page.Groups = new List<WizardOptionGroup>();
            page.Groups.Add(WizardOptionGroup.FromPMPGroup(pGroup, unzipPath));
            return page;
        }

    }

    /// <summary>
    /// Class representing the description/cover page of a Modpack.
    /// </summary>
    public class WizardMetaPage
    {
        public string Name;
        public string Author;
        public string Description;
        public string Url;
        public string Version;

        public static WizardMetaPage FromPMP(PMPJson pmp, string unzipPath)
        {
            var meta = pmp.Meta;
            var page = new WizardMetaPage();
            page.Url = meta.Website;
            page.Version = meta.Version;
            page.Author = meta.Author;
            page.Description = meta.Description;
            page.Name = meta.Name;
            return page;
        }

        public static WizardMetaPage FromWizardModpack(ModPackJson wiz, string unzipPath)
        {
            var page = new WizardMetaPage();
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
        public WizardMetaPage MetaPage;
        public List<WizardOptionsPage> OptionPages;
        public EModpackType ModpackType;
        public ModPack ModPack;
        public object RawSource;

        public static WizardData FromPmp(PMPJson pmp, string unzipPath)
        {
            var data = new WizardData();
            data.MetaPage = WizardMetaPage.FromPMP(pmp, unzipPath);
            data.OptionPages = new List<WizardOptionsPage>();
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
                    data.OptionPages.Add(WizardOptionsPage.FromPenumbraPage(g, unzipPath));
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

                data.OptionPages.Add(WizardOptionsPage.FromPenumbraPage(fakeGroup, unzipPath));
            }
            return data;
        }

        public static WizardData FromWizardPack(ModPackJson ttmp, string unzipPath)
        {
            var data = new WizardData();
            data.ModpackType = EModpackType.TtmpWizard;
            data.MetaPage = WizardMetaPage.FromWizardModpack(ttmp, unzipPath);

            var mp = new ModPack(null);
            mp.Author = data.MetaPage.Author;
            mp.Version = data.MetaPage.Version;
            mp.Name = data.MetaPage.Name;
            mp.Url = data.MetaPage.Url;
            data.ModPack = mp;
            data.RawSource = ttmp;

            data.OptionPages = new List<WizardOptionsPage>();
            foreach (var p in ttmp.ModPackPages)
            {
                data.OptionPages.Add(WizardOptionsPage.FromWizardModpackPage(p, unzipPath));
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
