using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views;
using ImageMagick;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Categories;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.Enums;

namespace FFXIV_TexTools.ViewModels
{
    class ModConverterViewModel
    {
        public ModConverterViewModel((ModPackJson ModPackJson, Dictionary<string, MagickImage> ImageDictionary) data) {
            this.TTMPData = data;
            LoadFromItemList();
        }
        public List<string> ItemTextList { get; set; }
        public List<IItem> ItemList { get; set; }
        public ObservableCollection<string> FromItemList { get; private set; } = new ObservableCollection<string>();
        public ObservableCollection<string> ConvertList { get; private set; } = new ObservableCollection<string>();
        public ObservableCollection<AutoCompleteEntry> ToConverterItemList { get; set; } = new ObservableCollection<AutoCompleteEntry>();
        public string SelectedFromItemText { get; set; }
        public int SelectedFromItemIndex { get; set; }
        public string SelectedConvertToItem { get; set; }
        private string _targetItemName;
        public string TTMPPath { get; set; }
        public string TargetItemName { get => _targetItemName; set { _targetItemName = value; NotifyPropertyChanged("TargetItemName"); } }
        public Func<string> GetNewModPackPath { get; set; }
        public Func<Task> ShowProgress { get; set; }
        public Func<Task> CloseProgress { get; set; }
        public Func<(int current, int total, string message), Task> ReportProgress { get; set; }
        Dictionary<string, string> _convertDic = new Dictionary<string, string>();
        public (ModPackJson ModPackJson, Dictionary<string, MagickImage> ImageDictionary) TTMPData { get; private set; }
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        List<List<ModsJson>> GetModsJsonList(ModPackJson json)
        {
            List<List<ModsJson>> modsJsonsList = new List<List<ModsJson>>();
            if (json.TTMPVersion.Contains("w"))
            {
                foreach (var page in json.ModPackPages)
                {
                    foreach (var g in page.ModGroups)
                    {
                        foreach (var o in g.OptionList)
                        {
                            modsJsonsList.Add(o.ModsJsons);
                        }
                    }
                }
            }
            else
            {
                modsJsonsList.Add(json.SimpleModsList);
            }
            return modsJsonsList;
        }
        void LoadFromItemList()
        {
            var modsJsonsList = GetModsJsonList(this.TTMPData.ModPackJson);
            FromItemList.Clear();
            var list = new List<ModsJson>();
            foreach(var it in modsJsonsList)
            {
                list.AddRange(it);
            }
            var query = list.Where(it => it.FullPath.EndsWith(".mdl")).Select(it => it.Name).Distinct();
            foreach (var item in query)
            {
                FromItemList.Add(item);
            }
        }
        public ICommand AddToConvertListCommand => new RelayCommand(AddToConvertList);
        private void AddToConvertList(object obj)
        {
            if (SelectedFromItemText==null|| SelectedFromItemText.Trim().Length == 0)
                return;
            if (SelectedFromItemText == TargetItemName)
                return;
            if (_convertDic.ContainsKey(SelectedFromItemText))
                return;
            if (!ItemList.Exists(it => it.Name == TargetItemName))
                return;
            var fromItem = ItemList.Single(it => it.Name == SelectedFromItemText);
            var targetItem = ItemList.Single(it => it.Name == TargetItemName);
            if (fromItem.ItemCategory != targetItem.ItemCategory)
                return;
            if ((fromItem as IItemModel).ModelInfo.ModelID == (targetItem as IItemModel).ModelInfo.ModelID)
                return;
            ConvertList.Add(SelectedFromItemText + "=>" + TargetItemName);
            _convertDic.Add(SelectedFromItemText, TargetItemName);
        }
        public ICommand RemoveFromConvertListCommand => new RelayCommand(RemoveFromConvertList);
        private void RemoveFromConvertList(object obj)
        {
            if (SelectedConvertToItem != null)
            {
                _convertDic.Remove(SelectedConvertToItem.Split(new char[] { '=', '>' })[0]);
                ConvertList.Remove(SelectedConvertToItem);
            }
        }
        public ICommand SetToConverterItemListCommand => new RelayCommand(SetToConverterItemList);
        private void SetToConverterItemList(object obj)
        {
            ToConverterItemList.Clear();
            var fromItem = ItemList.SingleOrDefault(it => it.Name == SelectedFromItemText);
            IEnumerable<string> query;
            if (fromItem == null)
            {
                query = ItemList.Select(it => it.Name);
            }
            else
            {
                query = ItemList.Where(
                    it => it.ItemCategory == fromItem.ItemCategory
                    &&it.Name!=fromItem.Name
                    &&(it as IItemModel).ModelInfo.ModelID!= (fromItem as IItemModel).ModelInfo.ModelID
                ).Select(it => it.Name);
            }
            foreach (var item in query)
            {
                ToConverterItemList.Add(new AutoCompleteEntry(item, item));
            }
        }
        public ICommand ConvertCommand => new RelayCommand(Convert);
        async void Convert(object obj)
        {
            if (_convertDic.Count == 0)
                return;
            var newModPackPath = GetNewModPackPath();
            if (newModPackPath == null)
                return;
            await this.ShowProgress();
            var modDataList = await GetModData(TTMPPath,TTMPData.ModPackJson);
            var gear = new Gear(new DirectoryInfo(Settings.Default.FFXIV_Directory), GetLanguage());

            foreach (var item in _convertDic)
            {
                var targetItemModel = ItemList.Single(it => it.Name == item.Value) as IItemModel;
                var xivGear = targetItemModel as XivGear;
                var raceListForMdl = await gear.GetRacesForModels(xivGear, targetItemModel.DataFile);
                var raceListForTex = await gear.GetRacesForTextures(xivGear, targetItemModel.DataFile);

                var jsonslist = GetModsJsonList(TTMPData.ModPackJson);
                var list = new List<ModsJson>();
                foreach(var it in jsonslist)
                {
                    list.AddRange(it);
                }
                var fromMdl = list.First(it => it.Name == item.Key && it.FullPath.EndsWith(".mdl"));
                var fromInfo = GetModelInfo(fromMdl.FullPath);

                var fromId = fromInfo.Id;
                var fromMdlRace = fromInfo.Race;

                var targetId = $"{fromId[0]}{targetItemModel.ModelInfo.ModelID.ToString().PadLeft(4, '0')}";
                var targetMdlRace = GetTargetRace(fromMdlRace, raceListForMdl);

                var sameModelList = ItemList.Where(
                    it => it.ItemCategory == targetItemModel.ItemCategory
                    && (it as IItemModel).ModelInfo.ModelID == targetItemModel.ModelInfo.ModelID
                    && (it as IItemModel).ModelInfo.Variant != targetItemModel.ModelInfo.Variant
                );
                
                var fromQuery = list.Where(it => it.Name == item.Key).ToList();
                var count = fromQuery.Count;
                var pv = 1;
                foreach(var fromItem in fromQuery)
                {
                    await this.ReportProgress((pv, count, $"{fromItem.Name}:{fromItem.FullPath}"));                   
                    if (fromItem.FullPath.EndsWith(".tex"))
                    {
                        var texInfo = GetTexInfo(fromItem.FullPath);
                        var race = GetTargetRace(texInfo.Race, raceListForTex);
                        fromItem.FullPath=fromItem.FullPath.Replace(fromId, targetId).Replace(texInfo.Race, race);
                        fromItem.Name = item.Value;
                        var fromTexList = jsonslist.Where(it => it.Exists(it2 => it2.FullPath == fromItem.FullPath));
                        foreach (var sameTex in sameModelList)
                        {
                            foreach (var texList in fromTexList)
                            {
                                var modsJson = new ModsJson();
                                modsJson.Category = fromItem.Category;
                                modsJson.DatFile = fromItem.DatFile;
                                modsJson.FullPath = fromItem.FullPath;
                                modsJson.ModOffset = fromItem.ModOffset;
                                modsJson.ModPackEntry = fromItem.ModPackEntry;
                                modsJson.ModSize = fromItem.ModSize;
                                modsJson.Name = fromItem.Name;
                                texList.Add(modsJson);
                            }
                        }
                    }
                    else if(fromItem.FullPath.EndsWith(".mtrl")){
                        var mtrlInfo = GetMtrlInfo(fromItem.FullPath);
                        var race = GetTargetRace(mtrlInfo.Race, raceListForTex);
                        fromItem.FullPath=fromItem.FullPath.Replace(fromId, targetId).Replace(mtrlInfo.Race, race);
                        fromItem.Name = item.Value;
                        var fromMtrlList = jsonslist.Where(it => it.Exists(it2 => it2.FullPath == fromItem.FullPath));
                        foreach (var sameMtrl in sameModelList)
                        {
                            foreach (var mtrlList in fromMtrlList)
                            {
                                var modsJson = new ModsJson();
                                modsJson.Category = fromItem.Category;
                                modsJson.DatFile = fromItem.DatFile;
                                modsJson.FullPath = fromItem.FullPath;
                                modsJson.ModOffset = fromItem.ModOffset;
                                modsJson.ModPackEntry = fromItem.ModPackEntry;
                                modsJson.ModSize = fromItem.ModSize;
                                modsJson.Name = fromItem.Name;
                                mtrlList.Add(modsJson);
                            }
                        }
                        var newMtrlData=await ConvertMtrlData(fromId,targetId,mtrlInfo.Race, race, await GetType2Data(modDataList[fromItem]));
                        modDataList[fromItem] = await CreateType2Data(newMtrlData);
                    }
                    else if (fromItem.FullPath.EndsWith(".mdl"))
                    {
                        var newMdlData= await ConvertMdlData(fromQuery, fromId, targetId, fromMdlRace, targetMdlRace, modDataList[fromItem]);
                        modDataList[fromItem] = await CreateType3Data(modDataList[fromItem], newMdlData.Data);
                        fromItem.FullPath = fromItem.FullPath.Replace(fromId, targetId).Replace(fromMdlRace, targetMdlRace);
                        fromItem.Name = item.Value;
                    }
                }
            }
            await CreateNewTTMP(newModPackPath,modDataList, TTMPData.ModPackJson);
            await this.CloseProgress();
        }
        (string Id,string Race) GetModelInfo(string fullPath)
        {
            var name = Path.GetFileNameWithoutExtension(fullPath);
            var tmps = fullPath.Split('/');
            return (Id:tmps[2],Race:name.Substring(0, 5));
        }
        (string Id, string Race) GetTexInfo(string fullPath)
        {
            var name = Path.GetFileNameWithoutExtension(fullPath);
            var tmps = fullPath.Split('/');
            return (Id: tmps[2], Race: name.Split('_')[1].Substring(0, 5));
        }
        (string Id, string Race,string Version) GetMtrlInfo(string fullPath)
        {
            var name = Path.GetFileNameWithoutExtension(fullPath);
            var tmps = fullPath.Split('/');
            return (Id: tmps[2], Race: name.Split('_')[1].Substring(0, 5),Version:tmps[tmps.Length-2]);
        }
        string GetTargetRace(string fromRace,List<XivRace> targetRaceList)
        {
            var race = fromRace;
            if (!targetRaceList.Exists(it => it.GetRaceCode() == fromRace.Substring(1, 4)))
            {
                var sex = int.Parse(fromRace.Substring(1, 2));
                if (sex > 14)
                    throw new Exception("race sex error");
                if (sex % 2 == 0)
                    sex = sex - 1;
                else
                    sex = sex + 1;
                race = $"{fromRace[0]}{sex}{fromRace.Substring(3,2)}";
                if (!targetRaceList.Exists(it => it.GetRaceCode() == race.Substring(1, 4)))
                {
                    throw new Exception("not found race");
                }
            }
            return race;
        }
        async Task<Dictionary<ModsJson,byte[]>> GetModData(string ttmpPath, ModPackJson modPackJson)
        {
            var result = new Dictionary<ModsJson,byte[]>();
            using (var archive = ZipFile.OpenRead(ttmpPath))
            {
                foreach (var zipEntry in archive.Entries)
                {
                    if (zipEntry.FullName.EndsWith(".mpd"))
                    {
                        var _tempMPD = Path.GetTempFileName();
                        using (var zipStream = zipEntry.Open())
                        {
                            using (var fileStream = new FileStream(_tempMPD, FileMode.OpenOrCreate))
                            {
                                await zipStream.CopyToAsync(fileStream);
                                using (var binaryReader = new BinaryReader(fileStream))
                                {
                                    var jsonsList= GetModsJsonList(modPackJson);
                                    var list = new List<ModsJson>();
                                    foreach(var it in jsonsList)
                                    {
                                        list.AddRange(it);
                                    }
                                    var query = list.OrderBy(it => it.ModOffset);
                                    var pv = 1;
                                    var count = query.Count();
                                    foreach (var item in query)
                                    {
                                        await this.ReportProgress((pv, count, $"{item.Name}:{item.FullPath}"));
                                        binaryReader.BaseStream.Seek(item.ModOffset, SeekOrigin.Begin);
                                        result.Add(item,binaryReader.ReadBytes(item.ModSize));
                                        pv++;
                                    }
                                }
                            }
                        }
                    }
                    else if (!zipEntry.FullName.EndsWith(".mpl"))
                    {
                        if(!File.Exists($"{Environment.GetEnvironmentVariable("TEMP")}\\{zipEntry.Name}"))
                            zipEntry.ExtractToFile($"{Environment.GetEnvironmentVariable("TEMP")}\\{zipEntry.Name}");
                    }
                }
            }
            return result;
        }
        async Task<byte[]> GetType2Data(byte[] datas)
        {
            var type2Bytes = new List<byte>();
            await Task.Run(async () =>
            {
                var offset = 0;
                using (var br = new BinaryReader(new MemoryStream(datas)))
                {
                    br.BaseStream.Seek(offset, SeekOrigin.Begin);

                    var headerLength = br.ReadInt32();

                    br.ReadBytes(16);

                    var dataBlockCount = br.ReadInt32();

                    for (var i = 0; i < dataBlockCount; i++)
                    {
                        br.BaseStream.Seek(offset + (24 + (8 * i)), SeekOrigin.Begin);

                        var dataBlockOffset = br.ReadInt32();

                        br.BaseStream.Seek(offset + headerLength + dataBlockOffset, SeekOrigin.Begin);

                        br.ReadBytes(8);

                        var compressedSize = br.ReadInt32();
                        var uncompressedSize = br.ReadInt32();

                        // When the compressed size of a data block shows 32000, it is uncompressed.
                        if (compressedSize == 32000)
                        {
                            type2Bytes.AddRange(br.ReadBytes(uncompressedSize));
                        }
                        else
                        {
                            var compressedData = br.ReadBytes(compressedSize);

                            var decompressedData = await IOUtil.Decompressor(compressedData, uncompressedSize);

                            type2Bytes.AddRange(decompressedData);
                        }
                    }
                }
            });

            return type2Bytes.ToArray();
        }
        async Task<byte[]> CreateType2Data(byte[] dataToCreate)
        {
            var newData = new List<byte>();
            var headerData = new List<byte>();
            var dataBlocks = new List<byte>();

            // Header size is defaulted to 128, but may need to change if the data being imported is very large.
            headerData.AddRange(BitConverter.GetBytes(128));
            headerData.AddRange(BitConverter.GetBytes(2));
            headerData.AddRange(BitConverter.GetBytes(dataToCreate.Length));

            var dataOffset = 0;
            var totalCompSize = 0;
            var uncompressedLength = dataToCreate.Length;

            var partCount = (int)Math.Ceiling(uncompressedLength / 16000f);

            headerData.AddRange(BitConverter.GetBytes(partCount));

            var remainder = uncompressedLength;

            using (var binaryReader = new BinaryReader(new MemoryStream(dataToCreate)))
            {
                binaryReader.BaseStream.Seek(0, SeekOrigin.Begin);

                for (var i = 1; i <= partCount; i++)
                {
                    if (i == partCount)
                    {
                        var compressedData = await IOUtil.Compressor(binaryReader.ReadBytes(remainder));
                        var padding = 128 - ((compressedData.Length + 16) % 128);

                        dataBlocks.AddRange(BitConverter.GetBytes(16));
                        dataBlocks.AddRange(BitConverter.GetBytes(0));
                        dataBlocks.AddRange(BitConverter.GetBytes(compressedData.Length));
                        dataBlocks.AddRange(BitConverter.GetBytes(remainder));
                        dataBlocks.AddRange(compressedData);
                        dataBlocks.AddRange(new byte[padding]);

                        headerData.AddRange(BitConverter.GetBytes(dataOffset));
                        headerData.AddRange(BitConverter.GetBytes((short)((compressedData.Length + 16) + padding)));
                        headerData.AddRange(BitConverter.GetBytes((short)remainder));

                        totalCompSize = dataOffset + ((compressedData.Length + 16) + padding);
                    }
                    else
                    {
                        var compressedData = await IOUtil.Compressor(binaryReader.ReadBytes(16000));
                        var padding = 128 - ((compressedData.Length + 16) % 128);

                        dataBlocks.AddRange(BitConverter.GetBytes(16));
                        dataBlocks.AddRange(BitConverter.GetBytes(0));
                        dataBlocks.AddRange(BitConverter.GetBytes(compressedData.Length));
                        dataBlocks.AddRange(BitConverter.GetBytes(16000));
                        dataBlocks.AddRange(compressedData);
                        dataBlocks.AddRange(new byte[padding]);

                        headerData.AddRange(BitConverter.GetBytes(dataOffset));
                        headerData.AddRange(BitConverter.GetBytes((short)((compressedData.Length + 16) + padding)));
                        headerData.AddRange(BitConverter.GetBytes((short)16000));

                        dataOffset += ((compressedData.Length + 16) + padding);
                        remainder -= 16000;
                    }
                }
            }

            headerData.InsertRange(12, BitConverter.GetBytes(totalCompSize / 128));
            headerData.InsertRange(16, BitConverter.GetBytes(totalCompSize / 128));

            var headerSize = 128;

            if (headerData.Count > 128)
            {
                headerData.RemoveRange(0, 4);
                headerData.InsertRange(0, BitConverter.GetBytes(256));
                headerSize = 256;
            }
            var headerPadding = headerSize - headerData.Count;

            headerData.AddRange(new byte[headerPadding]);

            newData.AddRange(headerData);
            newData.AddRange(dataBlocks);
            return newData.ToArray();
        }
        async Task<byte[]> ConvertMtrlData(string oldIdStr,string idStr,string oldRaceStr,string raceStr,byte[] mtrlData)
        {
            return await Task.Run(() => { 
                var newMtrlData = new byte[mtrlData.Length];
                mtrlData.CopyTo(newMtrlData, 0);
                using (var bw = new BinaryWriter(new MemoryStream(newMtrlData))) {
                    using (var br = new BinaryReader(new MemoryStream(mtrlData)))
                    {
                        br.BaseStream.Seek(12, SeekOrigin.Begin);
                        var textureCount = br.ReadByte();
                        var mapCount = br.ReadByte();
                        var colorSetCount = br.ReadByte();
                        var pathOffset = 16 + 4 * textureCount + 4 * mapCount + 4 * colorSetCount;
                        br.BaseStream.Seek(pathOffset, SeekOrigin.Begin);
                        for(var i = 0; i < textureCount; i++)
                        {
                            bw.BaseStream.Seek(br.BaseStream.Position, SeekOrigin.Begin);
                            List<byte> pathBytes = new List<byte>();
                            var b = br.ReadByte();
                            pathBytes.Add(b);
                            while (b != 0)
                            {
                                b = br.ReadByte();
                                pathBytes.Add(b);
                            }
                            var oldPath = Encoding.UTF8.GetString(pathBytes.ToArray());
                            var newPath = oldPath.Replace(oldIdStr,idStr).Replace(oldRaceStr,raceStr);
                            var newPathData = Encoding.UTF8.GetBytes(newPath);
                            bw.Write(newPathData);
                        }
                    }
                }
                return newMtrlData;
            });
        }
        async Task<(int MeshCount, int MaterialCount, byte[] Data)> GetType3Data(byte[] data)
        {
            var byteList = new List<byte>();
            var meshCount = 0;
            var materialCount = 0;

            await Task.Run(async () =>
            {
                var offset = 0;
                using (var br = new BinaryReader(new MemoryStream(data)))
                {
                    br.BaseStream.Seek(offset, SeekOrigin.Begin);

                    var headerLength = br.ReadInt32();
                    var fileType = br.ReadInt32();
                    var decompressedSize = br.ReadInt32();
                    var buffer1 = br.ReadInt32();
                    var buffer2 = br.ReadInt32();
                    var parts = br.ReadInt16();

                    var endOfHeader = offset + headerLength;

                    byteList.AddRange(new byte[68]);

                    br.BaseStream.Seek(offset + 24, SeekOrigin.Begin);

                    var chunkUncompSizes = new int[11];
                    var chunkLengths = new int[11];
                    var chunkOffsets = new int[11];
                    var chunkBlockStart = new int[11];
                    var chunkNumBlocks = new int[11];

                    for (var i = 0; i < 11; i++)
                    {
                        chunkUncompSizes[i] = br.ReadInt32();
                    }

                    for (var i = 0; i < 11; i++)
                    {
                        chunkLengths[i] = br.ReadInt32();
                    }

                    for (var i = 0; i < 11; i++)
                    {
                        chunkOffsets[i] = br.ReadInt32();
                    }

                    for (var i = 0; i < 11; i++)
                    {
                        chunkBlockStart[i] = br.ReadUInt16();
                    }

                    var totalBlocks = 0;
                    for (var i = 0; i < 11; i++)
                    {
                        chunkNumBlocks[i] = br.ReadUInt16();

                        totalBlocks += chunkNumBlocks[i];
                    }

                    meshCount = br.ReadUInt16();
                    materialCount = br.ReadUInt16();

                    br.ReadBytes(4);

                    var blockSizes = new int[totalBlocks];

                    for (var i = 0; i < totalBlocks; i++)
                    {
                        blockSizes[i] = br.ReadUInt16();
                    }

                    br.BaseStream.Seek(offset + headerLength + chunkOffsets[0], SeekOrigin.Begin);

                    for (var i = 0; i < totalBlocks; i++)
                    {
                        var lastPos = (int)br.BaseStream.Position;

                        br.ReadBytes(8);

                        var partCompSize = br.ReadInt32();
                        var partDecompSize = br.ReadInt32();

                        if (partCompSize == 32000)
                        {
                            byteList.AddRange(br.ReadBytes(partDecompSize));
                        }
                        else
                        {
                            var partDecompBytes = await IOUtil.Decompressor(br.ReadBytes(partCompSize), partDecompSize);

                            byteList.AddRange(partDecompBytes);
                        }

                        br.BaseStream.Seek(lastPos + blockSizes[i], SeekOrigin.Begin);
                    }
                }
            });

            return (meshCount, materialCount, byteList.ToArray());
        }
        async Task<byte[]> CreateType3Data(byte[] oldDatas,byte[] dataToCreate)
        {
            return await Task.Run(async () =>
            {
                var newDataList = new List<byte>();
                var offset = 0;
                using (var br = new BinaryReader(new MemoryStream(oldDatas)))
                {
                    using (var brForCreate = new BinaryReader(new MemoryStream(dataToCreate)))
                    {
                        br.BaseStream.Seek(offset, SeekOrigin.Begin);

                        var headerLength = br.ReadInt32();
                        var fileType = br.ReadInt32();
                        var decompressedSize = br.ReadInt32();
                        var buffer1 = br.ReadInt32();
                        var buffer2 = br.ReadInt32();
                        var parts = br.ReadInt16();

                        var endOfHeader = offset + headerLength;
                        br.BaseStream.Seek(offset + 24, SeekOrigin.Begin);
                        var chunkUncompSizes = new int[11];
                        var chunkLengths = new int[11];
                        var chunkOffsets = new int[11];
                        var chunkBlockStart = new int[11];
                        var chunkNumBlocks = new int[11];

                        for (var i = 0; i < 11; i++)
                        {
                            chunkUncompSizes[i] = br.ReadInt32();
                        }

                        for (var i = 0; i < 11; i++)
                        {
                            chunkLengths[i] = br.ReadInt32();
                        }

                        for (var i = 0; i < 11; i++)
                        {
                            chunkOffsets[i] = br.ReadInt32();
                        }

                        for (var i = 0; i < 11; i++)
                        {
                            chunkBlockStart[i] = br.ReadUInt16();
                        }

                        var totalBlocks = 0;
                        for (var i = 0; i < 11; i++)
                        {
                            chunkNumBlocks[i] = br.ReadUInt16();

                            totalBlocks += chunkNumBlocks[i];
                        }

                        var meshCount = br.ReadUInt16();
                        var materialCount = br.ReadUInt16();

                        br.ReadBytes(4);

                        var blockSizes = new int[totalBlocks];
                        var blockSizesOffset = br.BaseStream.Position;
                        for (var i = 0; i < totalBlocks; i++)
                        {
                            blockSizes[i] = br.ReadUInt16();
                        }
                        br.BaseStream.Seek(offset + headerLength + chunkOffsets[0], SeekOrigin.Begin);
                        var bakOffset = br.BaseStream.Position;
                        br.BaseStream.Position = 0;
                        newDataList.AddRange(br.ReadBytes((int)bakOffset));
                        brForCreate.BaseStream.Position += 68;
                        for (var i = 0; i < totalBlocks; i++)
                        {
                            var lastPos = (int)br.BaseStream.Position;

                            newDataList.AddRange(br.ReadBytes(8));

                            var partCompSize = br.ReadInt32();
                            var partDecompSize = br.ReadInt32();

                            var tmpBytes = brForCreate.ReadBytes(partDecompSize);
                            if (partCompSize == 32000)
                            {
                                newDataList.AddRange(BitConverter.GetBytes(partCompSize));
                                newDataList.AddRange(BitConverter.GetBytes(partDecompSize));
                                newDataList.AddRange(tmpBytes);
                                br.BaseStream.Position += partDecompSize;
                                newDataList.AddRange(new byte[blockSizes[i] - tmpBytes.Length - 16]);
                            }
                            else
                            {
                                var partCompBytes = await IOUtil.Compressor(tmpBytes);
                                newDataList.AddRange(BitConverter.GetBytes(partCompBytes.Length));
                                newDataList.AddRange(BitConverter.GetBytes(partDecompSize));
                                newDataList.AddRange(partCompBytes);
                                if (partCompBytes.Length + 16 > blockSizes[i])
                                    throw new Exception("size error");
                                br.BaseStream.Position += partCompSize;
                                newDataList.AddRange(new byte[blockSizes[i] - partCompBytes.Length - 16]);
                            }
                            br.BaseStream.Seek(lastPos + blockSizes[i], SeekOrigin.Begin);
                        }
                    }
                    newDataList.AddRange(br.ReadBytes((int)(br.BaseStream.Length-br.BaseStream.Position)));
                }
                return newDataList.ToArray();
            });
        }
        async Task<(int MeshCount, int MaterialCount, byte[] Data)> ConvertMdlData(IEnumerable<ModsJson> list,string oldIdStr, string idStr, string oldRaceStr, string raceStr, byte[] data)
        {
            return await Task.Run(async () => {
                var mdlData = await GetType3Data(data);
                var tmpData = new byte[mdlData.Data.Length];
                mdlData.Data.CopyTo(tmpData, 0);
                var newMdlData = (MeshCount:mdlData.MeshCount, MaterialCount:mdlData.MaterialCount, Data:tmpData);
                using (var bw = new BinaryWriter(new MemoryStream(newMdlData.Data)))
                {
                    using (var br = new BinaryReader(new MemoryStream(mdlData.Data)))
                    {
                        // We skip the Vertex Data Structures for now
                        // This is done so that we can get the correct number of meshes per LoD first
                        br.BaseStream.Seek(64 + 136 * mdlData.MeshCount + 4, SeekOrigin.Begin);
                        br.BaseStream.Position += 4;
                        var pathBlockSize = br.ReadInt32();
                        br.BaseStream.Position += pathBlockSize;
                        br.BaseStream.Position += 4;
                        var meshCount = br.ReadInt16();
                        var attributeCount = br.ReadInt16();
                        var meshPartCount = br.ReadInt16();
                        var materialCount = br.ReadInt16();
                        var boneCount = br.ReadInt16();
                        br.BaseStream.Seek(64 + 136 * mdlData.MeshCount + 4, SeekOrigin.Begin);
                        br.BaseStream.Position += 8;//to pathBlock
                        var skipCount = attributeCount + boneCount;
                        for (var i = 0; i < skipCount; i++)
                        {
                            var b = br.ReadByte();
                            while (b != 0)
                            {
                                b = br.ReadByte();
                            }
                        }
                        for (var i = 0; i < materialCount; i++)
                        {
                            bw.BaseStream.Seek(br.BaseStream.Position,SeekOrigin.Begin);
                            List<byte> pathBytes = new List<byte>();
                            var b = br.ReadByte();
                            pathBytes.Add(b);
                            while (b != 0)
                            {
                                b = br.ReadByte();
                                pathBytes.Add(b);
                            }
                            var oldPath = Encoding.UTF8.GetString(pathBytes.ToArray());
                            var newPath = oldPath.Replace(oldIdStr, idStr).Replace(oldRaceStr, raceStr);
                            if (list.Count(it => it.FullPath.Contains(oldPath.TrimEnd('\0'))) > 0 || list.Count(it => it.FullPath.Contains(newPath.TrimEnd('\0'))) > 0)
                            {
                                var newPathData = Encoding.UTF8.GetBytes(newPath);
                                bw.Write(newPathData);
                            }
                        }
                    }
                }
                return newMdlData;
            });
        }
        async Task CreateNewTTMP(string newModPackPath,Dictionary<ModsJson,byte[]> modDataList,ModPackJson modPackJson)
        {
            await Task.Run(async () =>
            {
                var tempMPD = Path.GetTempFileName();
                var tempMPL = Path.GetTempFileName();
                try
                {
                    using (var binaryWriter = new BinaryWriter(File.Open(tempMPD, FileMode.OpenOrCreate)))
                    {
                        var pv = 1;
                        var count = modDataList.Count;
                        foreach (var data in modDataList)
                        {
                            await this.ReportProgress((pv, count, $"{data.Key.Name}:{data.Key.FullPath}"));
                            binaryWriter.BaseStream.Seek(data.Key.ModOffset, SeekOrigin.Begin);
                            data.Key.ModSize = data.Value.Length;
                            binaryWriter.Write(data.Value);
                        }
                    }
                    File.WriteAllText(tempMPL, JsonConvert.SerializeObject(modPackJson));
                    if (File.Exists(newModPackPath))
                        File.Delete(newModPackPath);
                    using (var zip = ZipFile.Open(newModPackPath, ZipArchiveMode.Create))
                    {
                        zip.CreateEntryFromFile(tempMPL, "TTMPL.mpl");
                        zip.CreateEntryFromFile(tempMPD, "TTMPD.mpd");
                        foreach(var image in TTMPData.ImageDictionary)
                        {
                            zip.CreateEntryFromFile($"{Environment.GetEnvironmentVariable("TEMP")}\\{image.Key}",image.Key);
                        }
                    }
                }
                finally
                {
                    if (File.Exists(tempMPD))
                        File.Delete(tempMPD);
                    if (File.Exists(tempMPL))
                        File.Delete(tempMPL);
                }
            });
        }
        private static XivLanguage GetLanguage()
        {
            return XivLanguages.GetXivLanguage(Properties.Settings.Default.Application_Language);
        }
    }
}
